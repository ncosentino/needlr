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

// ============================================================================
// Code Review Pipeline — Phase + Stage Token Budget Composition
//
// Demonstrates how phases enforce budget boundaries at TWO levels:
//   - Phase budget: caps total tokens for all stages in the phase
//   - Stage budget: caps tokens for each individual stage within the phase
//
// Analysis phase:  10K per stage, 30K phase cap (3 stages)
// Synthesis phase: 30K per stage, 60K phase cap (2 stages)
// Reporting phase: 5K per stage, 10K phase cap (2 stages)
//
// If a single stage exceeds its ceiling, the stage fails — but other stages
// in the phase can still run. If the phase total is exceeded, remaining
// stages in the phase are cancelled.
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login` first)
// ============================================================================

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var copilotSection = configuration.GetSection("Copilot");
IChatClient chatClient = new CopilotChatClient(new CopilotChatClientOptions
{
    DefaultModel = copilotSection["Model"] ?? "claude-sonnet-4",
});

var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .UsingDiagnostics()
        .UsingTokenBudget())
    .UsingPostPluginRegistrationCallback(services =>
    {
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Warning));
    })
    .BuildServiceProvider(configuration);

var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();
var budgetTracker = serviceProvider.GetRequiredService<ITokenBudgetTracker>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();

// ── The diff to review ──────────────────────────────────────────────────
const string codeDiff = """
    diff --git a/src/Auth/TokenValidator.cs b/src/Auth/TokenValidator.cs
    @@ -12,6 +12,18 @@ public class TokenValidator
    +    private readonly ILogger _logger;
    +
    +    public TokenValidator(ILogger logger)
    +    {
    +        _logger = logger;
    +    }
    +
    +    public bool ValidateToken(string token)
    +    {
    +        if (token != null && token.Length > 0)
    +        {
    +            return token == _expectedToken;
    +        }
    +
    +        return false;
    +    }
    """;

// ── Create agents ───────────────────────────────────────────────────────
var patternAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "PatternDetector";
    o.Instructions = "Find anti-patterns and code smells in the diff. List each as: [SEVERITY] Description. Be concise.";
});

var securityAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "SecurityScanner";
    o.Instructions = "Find ONLY security vulnerabilities. Format: [SEVERITY] Description — Fix. If none, say 'No issues.'";
});

var styleAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "StyleChecker";
    o.Instructions = "Check for C# style issues (naming, formatting, modern syntax). List each as: [LOW] Description.";
});

var commentAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "CommentWriter";
    o.Instructions = "Generate a PR review comment in markdown. Sections: ## Summary, ## Findings, ## Recommendations. Under 200 words.";
});

var priorityAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "Prioritizer";
    o.Instructions = "Given findings, output a numbered list sorted by severity (highest first). One line per finding.";
});

var summaryAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "Summarizer";
    o.Instructions = "Write a one-paragraph summary (under 50 words) of the review findings.";
});

var formatAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "Formatter";
    o.Instructions = "Format the PR comment as a GitHub-flavored markdown comment block. Include the summary at the top.";
});

// ── Budget configuration ────────────────────────────────────────────────
// Phase budgets cap the TOTAL for all stages in the phase.
// Stage budgets cap EACH individual stage.
// Both compose: a stage can't exceed its own cap OR the remaining phase budget.

const long analysisPerStage = 10_000;
const long analysisPhaseCap = 30_000; // 3 stages × 10K
const long synthesisPerStage = 30_000;
const long synthesisPhaseCap = 60_000; // 2 stages × 30K
const long reportingPerStage = 5_000;
const long reportingPhaseCap = 10_000; // 2 stages × 5K

// ── Helper to persist output ────────────────────────────────────────────
static StageExecutionPolicy PersistAs(string fileName, long stageBudget) => new()
{
    TokenBudget = stageBudget,
    AfterExecution = (result, ctx) =>
    {
        if (result.ResponseText is not null)
        {
            ctx.Workspace.TryWriteFile(fileName, result.ResponseText);
        }

        return Task.CompletedTask;
    },
};

// ── Build the 3-phase pipeline ──────────────────────────────────────────
var phases = new[]
{
    // Phase 1: Analysis — 10K per stage, 30K phase cap
    new PipelinePhase("Analysis",
    [
        new PipelineStage("PatternDetection",
            new AgentStageExecutor(patternAgent, _ => $"Review this diff:\n{codeDiff}"),
            PersistAs("patterns.txt", analysisPerStage)),
        new PipelineStage("SecurityScan",
            new AgentStageExecutor(securityAgent, _ => $"Scan for security issues:\n{codeDiff}"),
            PersistAs("security.txt", analysisPerStage)),
        new PipelineStage("StyleCheck",
            new AgentStageExecutor(styleAgent, _ => $"Check style:\n{codeDiff}"),
            PersistAs("style.txt", analysisPerStage)),
    ],
    new PipelinePhasePolicy
    {
        TokenBudget = analysisPhaseCap,
        OnEnterAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔵 Phase {ctx.PhaseIndex + 1}/{ctx.TotalPhases}: {ctx.PhaseName}");
            Console.WriteLine($"   Budget: {analysisPerStage:N0}/stage, {analysisPhaseCap:N0}/phase");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   Phase consumed: {budgetTracker.CurrentTokens:N0} / {analysisPhaseCap:N0} tokens");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    }),

    // Phase 2: Synthesis — 30K per stage, 60K phase cap
    new PipelinePhase("Synthesis",
    [
        new PipelineStage("GenerateComments",
            new AgentStageExecutor(commentAgent, ctx =>
            {
                var patterns = ctx.Workspace.TryReadFile("patterns.txt").Value.Content;
                var security = ctx.Workspace.TryReadFile("security.txt").Value.Content;
                var style = ctx.Workspace.TryReadFile("style.txt").Value.Content;
                return $"Write PR comment from:\nPatterns:\n{patterns}\nSecurity:\n{security}\nStyle:\n{style}";
            }),
            PersistAs("comments.txt", synthesisPerStage)),
        new PipelineStage("PrioritizeFindings",
            new AgentStageExecutor(priorityAgent, ctx =>
            {
                var patterns = ctx.Workspace.TryReadFile("patterns.txt").Value.Content;
                var security = ctx.Workspace.TryReadFile("security.txt").Value.Content;
                return $"Prioritize these findings:\n{patterns}\n{security}";
            }),
            PersistAs("priorities.txt", synthesisPerStage)),
    ],
    new PipelinePhasePolicy
    {
        TokenBudget = synthesisPhaseCap,
        OnEnterAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n🟡 Phase {ctx.PhaseIndex + 1}/{ctx.TotalPhases}: {ctx.PhaseName}");
            Console.WriteLine($"   Budget: {synthesisPerStage:N0}/stage, {synthesisPhaseCap:N0}/phase");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   Phase consumed: {budgetTracker.CurrentTokens:N0} / {synthesisPhaseCap:N0} tokens");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    }),

    // Phase 3: Reporting — 5K per stage, 10K phase cap
    new PipelinePhase("Reporting",
    [
        new PipelineStage("FormatPRComment",
            new AgentStageExecutor(formatAgent, ctx =>
            {
                var comments = ctx.Workspace.TryReadFile("comments.txt").Value.Content;
                var summary = ctx.Workspace.TryReadFile("priorities.txt").Value.Content;
                return $"Format as GitHub PR comment:\n{comments}\n\nPriorities:\n{summary}";
            }),
            PersistAs("pr-comment.md", reportingPerStage)),
        new PipelineStage("GenerateSummary",
            new AgentStageExecutor(summaryAgent, ctx =>
            {
                var priorities = ctx.Workspace.TryReadFile("priorities.txt").Value.Content;
                return $"Summarize in one paragraph under 50 words:\n{priorities}";
            }),
            PersistAs("summary.txt", reportingPerStage)),
    ],
    new PipelinePhasePolicy
    {
        TokenBudget = reportingPhaseCap,
        OnEnterAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n🟢 Phase {ctx.PhaseIndex + 1}/{ctx.TotalPhases}: {ctx.PhaseName}");
            Console.WriteLine($"   Budget: {reportingPerStage:N0}/stage, {reportingPhaseCap:N0}/phase");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   Phase consumed: {budgetTracker.CurrentTokens:N0} / {reportingPhaseCap:N0} tokens");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    }),
};

// ── Banner ──────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Code Review Pipeline — Phase + Stage Budget Composition    ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Analysis:  10K/stage, 30K/phase  (3 stages)               ║");
Console.WriteLine("║  Synthesis: 30K/stage, 60K/phase  (2 stages)               ║");
Console.WriteLine("║  Reporting:  5K/stage, 10K/phase  (2 stages)               ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("📋 Reviewing: src/Auth/TokenValidator.cs (+16 lines)");
Console.WriteLine();

// ── Run ─────────────────────────────────────────────────────────────────
var workspace = new InMemoryWorkspace();
var runner = new SequentialPipelineRunner(diagnosticsAccessor, budgetTracker, progressFactory);

var result = await runner.RunPhasedAsync(workspace, phases, options: null, CancellationToken.None);

// ── Per-stage diagnostics ───────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("╔═════════════════════╦═══════════╦══════════╦══════════╦═══════════╗");
Console.WriteLine("║ Stage               ║ Phase     ║ Input Tk ║ Out Tk   ║ Duration  ║");
Console.WriteLine("╠═════════════════════╬═══════════╬══════════╬══════════╬═══════════╣");

foreach (var stage in result.Stages)
{
    var diag = stage.Diagnostics;
    var name = stage.AgentName;
    var phase = stage.PhaseName ?? "-";
    var inputTk = diag?.AggregateTokenUsage.InputTokens.ToString("N0") ?? "-";
    var outputTk = diag?.AggregateTokenUsage.OutputTokens.ToString("N0") ?? "-";
    var duration = diag is not null ? $"{diag.TotalDuration.TotalSeconds:F1}s" : "-";
    Console.WriteLine($"║ {name,-19} ║ {phase,-9} ║ {inputTk,8} ║ {outputTk,8} ║ {duration,9} ║");
}

Console.WriteLine("╚═════════════════════╩═══════════╩══════════╩══════════╩═══════════╝");

// ── Summary ─────────────────────────────────────────────────────────────
Console.WriteLine();
Console.ForegroundColor = result.Succeeded ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"Pipeline: {(result.Succeeded ? "SUCCESS" : $"FAILED: {result.ErrorMessage}")}");
Console.ResetColor();
Console.WriteLine($"Duration: {result.TotalDuration.TotalSeconds:F1}s | Tokens: {result.AggregateTokenUsage?.TotalTokens ?? 0:N0}");

Console.WriteLine();
Console.WriteLine("═══ Budget Usage by Phase ═══");
foreach (var group in result.Stages.GroupBy(s => s.PhaseName))
{
    var phaseTokens = group.Sum(s => s.Diagnostics?.AggregateTokenUsage.TotalTokens ?? 0);
    Console.WriteLine($"  {group.Key}: {phaseTokens:N0} tokens");
}

// ── Show the generated PR comment ───────────────────────────────────────
var prComment = workspace.TryReadFile("pr-comment.md");
if (prComment.Value.Content is not null)
{
    Console.WriteLine();
    Console.WriteLine("═══ Generated PR Comment ═══");
    Console.WriteLine(prComment.Value.Content);
}
