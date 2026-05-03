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

using SupportTriagePipelineApp;

// ============================================================================
// Customer Support Triage — 3-Phase Agent Pipeline
//
// Demonstrates what pipeline phases add beyond a flat stage list:
//   - OnEnterAsync: lifecycle hooks that fire at phase boundaries
//   - Typed state: TriageState flows urgency score between phases
//   - ShouldSkip: escalation phase is conditionally skipped based on
//     typed state (not string parsing of workspace files)
//   - PostValidation + retry: urgency stage retries if response
//     doesn't contain a parseable score
//   - CompletionGate: pipeline fails if classification produced no
//     valid urgency score
//   - Workspace-as-memory: agent outputs flow between phases via files
//   - Per-phase diagnostics: token usage grouped by phase in output
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login`)
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

// ── The customer ticket ─────────────────────────────────────────────────
const string ticket = "My order hasn't arrived and it's been 3 weeks. " +
    "Order #A-29481. I've already contacted you twice with no resolution. " +
    "I need this resolved today or I want a full refund.";

// ── Create focused agents ───────────────────────────────────────────────
var intentAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "IntentDetector";
    o.Instructions = "You are an intent classifier. Output ONLY the intent label, nothing else. " +
        "Valid labels: billing, shipping_delay, product_defect, account_access, general_inquiry";
});

var urgencyAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "UrgencyScorer";
    o.Instructions = "You are an urgency scorer. Output ONLY a single digit 1-5, nothing else. " +
        "1=low 2=minor 3=moderate 4=high 5=critical";
});

var draftAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "ResponseDrafter";
    o.Instructions = """
        Draft a customer support response. Write a helpful, empathetic response
        under 150 words. Be specific and actionable — reference the order number,
        acknowledge previous contacts, and provide a concrete next step.
        """;
});

var factCheckAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "FactChecker";
    o.Instructions = """
        Verify the draft response. Check that it addresses the customer's specific
        concern, doesn't make unrealistic promises, and has an appropriate tone.
        Respond with EXACTLY one line: PASS or FAIL followed by a brief reason.
        """;
});

var supervisorAgent = agentFactory.CreateAgent(o =>
{
    o.Name = "Supervisor";
    o.Instructions = """
        You are a senior support supervisor reviewing a high-urgency ticket.
        Review the draft response and fact-check result. If good, approve it.
        If not, rewrite it. Respond with:
        Decision: APPROVED or REWRITTEN
        Response: <the final response to send to the customer>
        """;
});

// ── Helper to persist agent output to workspace ─────────────────────────
static StageExecutionPolicy PersistAs(string fileName) => new()
{
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
var triageState = new TriageState();

var phases = new[]
{
    new PipelinePhase("Classification",
    [
        new PipelineStage("IntentDetection",
            new AgentStageExecutor(intentAgent, _ => $"Classify:\n{ticket}"),
            PersistAs("intent.txt")),
        new PipelineStage("UrgencyScoring",
            new AgentStageExecutor(urgencyAgent, _ => $"Score urgency:\n{ticket}"),
            new StageExecutionPolicy
            {
                MaxAttempts = 2,
                PostValidation = result =>
                {
                    if (triageState.TryParseUrgency(result.ResponseText))
                    {
                        return null;
                    }

                    return "Could not parse urgency score from response — retrying";
                },
                AfterExecution = (result, ctx) =>
                {
                    if (result.ResponseText is not null)
                    {
                        ctx.Workspace.TryWriteFile("urgency.txt", result.ResponseText);
                    }

                    return Task.CompletedTask;
                },
            }),
    ],
    new PipelinePhasePolicy
    {
        OnEnterAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔵 Phase {ctx.PhaseIndex + 1}/{ctx.TotalPhases}: {ctx.PhaseName}");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, _) =>
        {
            var state = ctx.GetRequiredState<TriageState>();
            var label = state.UrgencyScore switch
            {
                >= 4 => "HIGH — will escalate",
                >= 2 => "MEDIUM — no escalation",
                > 0 => "LOW — no escalation",
                _ => "UNKNOWN — escalation will be skipped",
            };
            Console.WriteLine($"  Urgency result: {state.UrgencyScore?.ToString() ?? "?"}/5 ({label})");
            return ValueTask.CompletedTask;
        },
    }),

    new PipelinePhase("Resolution",
    [
        new PipelineStage("DraftResponse",
            new AgentStageExecutor(draftAgent, ctx =>
            {
                var intent = ctx.Workspace.TryReadFile("intent.txt").Value.Content;
                return $"Draft a response.\nClassification: {intent}\nTicket: {ticket}";
            }),
            PersistAs("draft.txt")),
        new PipelineStage("FactCheck",
            new AgentStageExecutor(factCheckAgent, ctx =>
            {
                var draft = ctx.Workspace.TryReadFile("draft.txt").Value.Content;
                return $"Verify this draft:\n{draft}\n\nOriginal ticket: {ticket}";
            }),
            PersistAs("factcheck.txt")),
    ],
    new PipelinePhasePolicy
    {
        OnEnterAsync = (ctx, _) =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n🟡 Phase {ctx.PhaseIndex + 1}/{ctx.TotalPhases}: {ctx.PhaseName}");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    }),

    new PipelinePhase("Escalation",
    [
        new PipelineStage("SupervisorReview",
            new AgentStageExecutor(supervisorAgent, ctx =>
            {
                var draft = ctx.Workspace.TryReadFile("draft.txt").Value.Content;
                var check = ctx.Workspace.TryReadFile("factcheck.txt").Value.Content;
                return $"Review:\nDraft: {draft}\nFact-check: {check}\nTicket: {ticket}";
            }),
            new StageExecutionPolicy
            {
                ShouldSkip = ctx =>
                {
                    var state = ctx.GetRequiredState<TriageState>();
                    if (state.UrgencyScore is null or < 4)
                    {
                        Console.WriteLine("  ⏭ Skipping — urgency below escalation threshold");
                        return true;
                    }

                    return false;
                },
                AfterExecution = (result, ctx) =>
                {
                    if (result.ResponseText is not null)
                    {
                        ctx.Workspace.TryWriteFile("supervisor.txt", result.ResponseText);
                    }

                    return Task.CompletedTask;
                },
            }),
    ],
    new PipelinePhasePolicy
    {
        OnEnterAsync = (ctx, _) =>
        {
            var state = ctx.GetRequiredState<TriageState>();
            var reason = state.UrgencyScore >= 4
                ? $"urgency {state.UrgencyScore}/5 — escalating"
                : "urgency below threshold";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n🔴 Phase {ctx.PhaseIndex + 1}/{ctx.TotalPhases}: {ctx.PhaseName} ({reason})");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    }),
};

// ── Banner ──────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Customer Support Triage — 3-Phase Agent Pipeline           ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Features demonstrated:                                      ║");
Console.WriteLine("║    • OnEnterAsync / OnExitAsync lifecycle hooks               ║");
Console.WriteLine("║    • Typed pipeline state (TriageState) across phases         ║");
Console.WriteLine("║    • ShouldSkip driven by typed state                         ║");
Console.WriteLine("║    • PostValidation + retry on urgency parsing                ║");
Console.WriteLine("║    • Workspace-as-memory between phases                       ║");
Console.WriteLine("║    • CompletionGate for pipeline-level validation             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"📋 Ticket: \"{ticket[..60]}...\"");
Console.WriteLine();

// ── Run ─────────────────────────────────────────────────────────────────
var workspace = new InMemoryWorkspace();
var runner = new SequentialPipelineRunner(diagnosticsAccessor, budgetTracker, progressFactory);

var options = new SequentialPipelineOptions
{
    CompletionGate = _ =>
    {
        if (triageState.UrgencyScore is null)
        {
            return "Classification phase failed to produce a valid urgency score";
        }

        return null;
    },
};

var result = await runner.RunPhasedAsync(workspace, phases, triageState, options, CancellationToken.None);

// ── Per-stage diagnostics ───────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("╔═════════════════════╦════════════════╦══════════╦══════════╦═══════════╗");
Console.WriteLine("║ Stage               ║ Phase          ║ Input Tk ║ Out Tk   ║ Duration  ║");
Console.WriteLine("╠═════════════════════╬════════════════╬══════════╬══════════╬═══════════╣");

foreach (var stage in result.Stages)
{
    var diag = stage.Diagnostics;
    var name = stage.AgentName;
    var phase = stage.PhaseName ?? "-";
    var inputTk = diag?.AggregateTokenUsage.InputTokens > 0
        ? diag.AggregateTokenUsage.InputTokens.ToString("N0") : "N/A";
    var outputTk = diag?.AggregateTokenUsage.OutputTokens > 0
        ? diag.AggregateTokenUsage.OutputTokens.ToString("N0") : "N/A";
    var duration = diag is not null ? $"{diag.TotalDuration.TotalSeconds:F1}s" : "-";
    var skip = stage.Outcome == StageOutcome.Skipped ? " [SKIP]" : "";
    Console.WriteLine($"║ {name + skip,-19} ║ {phase,-14} ║ {inputTk,8} ║ {outputTk,8} ║ {duration,9} ║");
}

Console.WriteLine("╚═════════════════════╩════════════════╩══════════╩══════════╩═══════════╝");

// ── Summary ─────────────────────────────────────────────────────────────
Console.WriteLine();
Console.ForegroundColor = result.Succeeded ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"Pipeline: {(result.Succeeded ? "SUCCESS" : $"FAILED: {result.ErrorMessage}")}");
Console.ResetColor();
Console.WriteLine($"Duration: {result.TotalDuration.TotalSeconds:F1}s | " +
    $"Tokens: {result.AggregateTokenUsage?.TotalTokens ?? 0:N0}");
Console.WriteLine($"Urgency: {triageState.UrgencyScore?.ToString() ?? "not parsed"}/5");

// ── Per-phase token costs ───────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("═══ Tokens by Phase ═══");
foreach (var group in result.Stages.GroupBy(s => s.PhaseName))
{
    var phaseTokens = group.Sum(s => s.Diagnostics?.AggregateTokenUsage.TotalTokens ?? 0);
    var stageCount = group.Count(s => s.Outcome != StageOutcome.Skipped);
    Console.WriteLine($"  {group.Key}: {phaseTokens:N0} tokens across {stageCount} stage(s)");
}

// ── Workspace outputs ───────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("═══ Agent Outputs ═══");
foreach (var file in workspace.GetFilePaths().OrderBy(f => f))
{
    var content = workspace.TryReadFile(file).Value.Content;
    Console.WriteLine($"\n📄 {file}:");
    Console.WriteLine(content);
}
