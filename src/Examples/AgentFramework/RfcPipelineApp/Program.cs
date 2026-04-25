using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.Copilot;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using RfcPipelineApp.Core;

// ============================================================================
// RFC Pipeline — Console Entry Point
//
// Demonstrates a 16-stage SequentialPipelineRunner that converts a feature
// request into a complete RFC/design document. Stages mix agent-driven
// (LLM) steps with programmatic gates and validators.
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login` first)
// ============================================================================

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// ── LLM provider (Copilot) ─────────────────────────────────────────────
var copilotSection = configuration.GetSection("Copilot");
var copilotOptions = new CopilotChatClientOptions
{
    DefaultModel = copilotSection["Model"] ?? "claude-sonnet-4",
};
IChatClient chatClient = new CopilotChatClient(copilotOptions);

// ── Needlr DI setup ────────────────────────────────────────────────────
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .UsingDiagnostics()
        .UsingTokenBudget())
    .UsingPostPluginRegistrationCallback(services =>
    {
        services.AddLogging(builder => builder.AddConsole());
    })
    .BuildServiceProvider(configuration);

var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();
var budgetTracker = serviceProvider.GetRequiredService<ITokenBudgetTracker>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("RfcPipeline");

// ── Define the feature request ──────────────────────────────────────────
var assignment = new RfcAssignment(
    FeatureTitle: "Workspace File Versioning",
    Description: """
        Add automatic versioning to the InMemoryWorkspace so that every write
        creates a new version. Agents can read previous versions, diff between
        versions, and roll back to a prior state. This enables undo/redo in
        multi-stage pipelines and makes it possible to audit how a document
        evolved across pipeline stages.
        """,
    Constraints:
    [
        "Must be backward-compatible — existing IWorkspace consumers must not break",
        "Memory overhead must be bounded (configurable max versions per file)",
        "Thread-safe for concurrent agent access",
        "Must not require changes to the IWorkspace interface (additive only)",
    ],
    ExistingContext:
    [
        "InMemoryWorkspace uses ConcurrentDictionary<string, string> today",
        "IWorkspace has TryReadFile, TryWriteFile, FileExists, GetFilePaths, CompareExchange",
        "Pipelines run stages sequentially but agents within a stage may be concurrent",
        "The SequentialPipelineRunner already captures per-stage diagnostics",
    ],
    TargetAudience: "engineering team");

// ── Build pipeline stages ───────────────────────────────────────────────
var metadata = new RfcMetadata();
var state = new RfcPipelineState(metadata);
var stages = RfcPipelineBuilder.Build(assignment, agentFactory, state, logger);

// ── Create workspace ────────────────────────────────────────────────────
var workspace = new InMemoryWorkspace();

// ── Print banner ────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         RFC PIPELINE — 16-Stage Sequential Pipeline         ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Feature:    {assignment.FeatureTitle,-47}║");
Console.WriteLine($"║  Stages:     {stages.Count,-47}║");
Console.WriteLine($"║  Audience:   {assignment.TargetAudience,-47}║");
Console.WriteLine($"║  LLM:        Copilot ({copilotOptions.DefaultModel}){"",-25}║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

Console.WriteLine("Pipeline stages:");
for (var i = 0; i < stages.Count; i++)
{
    var stage = stages[i];
    var executorType = stage.Executor switch
    {
        AgentStageExecutor => "Agent",
        DelegateStageExecutor => "Delegate",
        ContinueOnFailureExecutor => "Agent+Advisory",
        CritiqueAndReviseExecutor => "CritiqueAndRevise",
        _ => stage.Executor.GetType().Name,
    };
    Console.WriteLine($"  {i + 1,2}. [{executorType,-18}] {stage.Name}");
}
Console.WriteLine();

// ── Run pipeline ────────────────────────────────────────────────────────
var runner = new SequentialPipelineRunner(diagnosticsAccessor, budgetTracker, progressFactory);

Console.WriteLine("Starting pipeline execution...");
Console.WriteLine();

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var result = await runner.RunAsync(workspace, stages, state, options: null, CancellationToken.None);
stopwatch.Stop();

// ── Print per-stage diagnostics ─────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                         PER-STAGE DIAGNOSTICS                               ║");
Console.WriteLine("╠═══════════════════════════╦══════════╦══════════╦══════════╦═════════════════╣");
Console.WriteLine("║ Stage                     ║ Input Tk ║ Out Tk   ║ Tools    ║ Duration        ║");
Console.WriteLine("╠═══════════════════════════╬══════════╬══════════╬══════════╬═════════════════╣");

foreach (var stage in result.Stages)
{
    var diag = stage.Diagnostics;
    var name = stage.AgentName.Length > 25 ? stage.AgentName[..22] + "..." : stage.AgentName;
    var inputTk = diag?.AggregateTokenUsage.InputTokens.ToString("N0") ?? "-";
    var outputTk = diag?.AggregateTokenUsage.OutputTokens.ToString("N0") ?? "-";
    var tools = diag?.ToolCalls.Count.ToString() ?? "-";
    var duration = diag?.TotalDuration.TotalSeconds.ToString("F1") + "s" ?? "-";
    Console.WriteLine($"║ {name,-25} ║ {inputTk,8} ║ {outputTk,8} ║ {tools,8} ║ {duration,15} ║");
}

Console.WriteLine("╠═══════════════════════════╬══════════╬══════════╬══════════╬═════════════════╣");

var aggTokens = result.AggregateTokenUsage;
var totalInput = aggTokens?.InputTokens.ToString("N0") ?? "-";
var totalOutput = aggTokens?.OutputTokens.ToString("N0") ?? "-";
Console.WriteLine($"║ {"TOTAL",-25} ║ {totalInput,8} ║ {totalOutput,8} ║ {"",8} ║ {stopwatch.Elapsed.TotalSeconds,13:F1}s ║");
Console.WriteLine("╚═══════════════════════════╩══════════╩══════════╩══════════╩═════════════════╝");
Console.WriteLine();

// ── Print result summary ────────────────────────────────────────────────
Console.ForegroundColor = result.Succeeded ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"Pipeline result: {(result.Succeeded ? "SUCCESS" : $"FAILED: {result.ErrorMessage}")}");
Console.ResetColor();
Console.WriteLine($"Total duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"Stages completed: {result.Stages.Count}");
Console.WriteLine();

// ── Print pipeline state summary (typed state) ─────────────────────────
Console.WriteLine("═══ Pipeline State Summary ═══");
Console.WriteLine($"  Structure validation: {(state.StructureValidationPassed ? "PASS" : "FAIL")}");
Console.WriteLine($"  Technical review: {(state.TechnicalReviewPassed ? "PASS" : "FAIL/SKIPPED")}");
Console.WriteLine($"  Cold reader: {(state.ColdReaderPassed ? "PASS" : "FAIL")} ({state.ColdReaderAttempts} attempts)");
Console.WriteLine($"  Review findings: {state.ReviewFindings.Count}");
Console.WriteLine($"  Applied fixes: {state.AppliedFixes.Count}");
Console.WriteLine();

// ── Print metadata ──────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                      RFC METADATA                           ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Title:   {metadata.Title,-50}║");
Console.WriteLine($"║  Status:  {metadata.Status,-50}║");
Console.WriteLine($"║  Authors: {string.Join(", ", metadata.Authors),-50}║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
var summaryLines = WordWrap(metadata.Summary, 58);
foreach (var line in summaryLines)
{
    Console.WriteLine($"║  {line,-58}║");
}
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── Print workspace files ───────────────────────────────────────────────
Console.WriteLine("═══ Final Workspace Files ═══");
foreach (var file in workspace.GetFilePaths().OrderBy(f => f))
{
    var content = workspace.TryReadFile(file).Value.Content;
    Console.WriteLine($"  📄 {file} ({content.Length:N0} chars)");

    if (file == assignment.DraftPath)
    {
        // Print the full RFC draft
        Console.WriteLine();
        Console.WriteLine("═══ GENERATED RFC DOCUMENT ═══");
        Console.WriteLine(content);
        Console.WriteLine("═══ END RFC DOCUMENT ═══");
    }
    else
    {
        var preview = content.Length > 200 ? content[..197] + "..." : content;
        foreach (var line in preview.Split('\n').Take(5))
        {
            Console.WriteLine($"     {line.TrimEnd()}");
        }
    }
    Console.WriteLine();
}

static IReadOnlyList<string> WordWrap(string text, int maxWidth)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return ["(none)"];
    }

    var lines = new List<string>();
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var current = "";

    foreach (var word in words)
    {
        if (current.Length + word.Length + 1 > maxWidth)
        {
            lines.Add(current);
            current = word;
        }
        else
        {
            current = current.Length == 0 ? word : $"{current} {word}";
        }
    }

    if (current.Length > 0)
    {
        lines.Add(current);
    }

    return lines;
}
