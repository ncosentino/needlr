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
// Customer Support Triage — 3-Phase Pipeline
//
// Demonstrates Needlr's PipelinePhase API by grouping stages into named
// phases with lifecycle hooks, typed shared state, and conditional skipping.
//
// Key features shown:
//   - PipelinePhase / PipelinePhasePolicy for grouping stages
//   - OnEnterAsync / OnExitAsync lifecycle hooks
//   - ShouldSkip on StageExecutionPolicy for conditional execution
//   - Typed pipeline state (SupportTicketState) shared across all stages
//   - DelegateStageExecutor for zero-boilerplate programmatic stages
//
// No LLM calls are made — all stage logic is simulated so the example
// runs instantly without API keys.
// ============================================================================

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// ── LLM provider (Copilot — not used in this demo, but wired for realism) ──
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

var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();
var budgetTracker = serviceProvider.GetRequiredService<ITokenBudgetTracker>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();

// ── Define the support ticket ───────────────────────────────────────────
var state = new SupportTicketState
{
    CustomerMessage = "My order hasn't arrived and it's been 3 weeks",
};

// ── Build phased pipeline ───────────────────────────────────────────────
// Phase 1: Classification — fast/cheap model, detect intent and urgency
var classificationPhase = new PipelinePhase(
    "Classification",
    [
        new PipelineStage(
            "IntentDetection",
            new DelegateStageExecutor(async (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<SupportTicketState>();
                s.DetectedIntent = "shipping_delay";
                Console.WriteLine($"  ✓ IntentDetection — intent: {s.DetectedIntent}");
                await Task.CompletedTask;
            })),
        new PipelineStage(
            "UrgencyScoring",
            new DelegateStageExecutor(async (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<SupportTicketState>();
                s.UrgencyScore = 4;
                var label = s.UrgencyScore >= 4 ? "HIGH" : s.UrgencyScore >= 2 ? "MEDIUM" : "LOW";
                Console.WriteLine($"  ✓ UrgencyScoring — urgency: {s.UrgencyScore}/5 ({label})");
                await Task.CompletedTask;
            })),
    ],
    new PipelinePhasePolicy
    {
        OnEnterAsync = (ctx, ct) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔵 Phase {ctx.PhaseIndex + 1}: {ctx.PhaseName}");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    });

// Phase 2: Resolution — standard model, search + draft + verify
var resolutionPhase = new PipelinePhase(
    "Resolution",
    [
        new PipelineStage(
            "KnowledgeSearch",
            new DelegateStageExecutor(async (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<SupportTicketState>();
                s.ArticlesFound = 3;
                Console.WriteLine($"  ✓ KnowledgeSearch — found {s.ArticlesFound} relevant articles");
                await Task.CompletedTask;
            })),
        new PipelineStage(
            "DraftResponse",
            new DelegateStageExecutor(async (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<SupportTicketState>();
                s.DraftWordCount = 142;
                ctx.Workspace.TryWriteFile("draft-response.md",
                    "We apologize for the delay with your order...");
                Console.WriteLine($"  ✓ DraftResponse — drafted response ({s.DraftWordCount} words)");
                await Task.CompletedTask;
            })),
        new PipelineStage(
            "FactCheck",
            new DelegateStageExecutor(async (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<SupportTicketState>();
                s.FactCheckPassed = true;
                Console.WriteLine($"  ✓ FactCheck — all claims verified");
                await Task.CompletedTask;
            })),
    ],
    new PipelinePhasePolicy
    {
        OnEnterAsync = (ctx, ct) =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n🟡 Phase {ctx.PhaseIndex + 1}: {ctx.PhaseName}");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    });

// Phase 3: Escalation — premium model, only runs if urgency >= 4
// Demonstrates ShouldSkip: stages are skipped when urgency is low.
var escalationSkipPolicy = new StageExecutionPolicy
{
    ShouldSkip = ctx =>
    {
        var s = ctx.GetRequiredState<SupportTicketState>();
        return s.UrgencyScore < 4;
    },
};

var escalationPhase = new PipelinePhase(
    "Escalation",
    [
        new PipelineStage(
            "SupervisorReview",
            new DelegateStageExecutor(async (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<SupportTicketState>();
                s.SupervisorApproved = true;
                Console.WriteLine($"  ✓ SupervisorReview — approved with minor edits");
                await Task.CompletedTask;
            }),
            escalationSkipPolicy),
        new PipelineStage(
            "HandoffPreparation",
            new DelegateStageExecutor(async (ctx, ct) =>
            {
                var s = ctx.GetRequiredState<SupportTicketState>();
                s.HandoffReady = true;
                ctx.Workspace.TryWriteFile("handoff-notes.md",
                    $"Escalation: {s.DetectedIntent}, urgency {s.UrgencyScore}/5");
                Console.WriteLine($"  ✓ HandoffPreparation — handoff notes ready");
                await Task.CompletedTask;
            }),
            escalationSkipPolicy),
    ],
    new PipelinePhasePolicy
    {
        OnEnterAsync = (ctx, ct) =>
        {
            var s = ctx.GetRequiredState<SupportTicketState>();
            var reason = s.UrgencyScore >= 4
                ? $"(urgency >= 4)"
                : "(skipping — urgency below threshold)";

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n🔴 Phase {ctx.PhaseIndex + 1}: {ctx.PhaseName} {reason}");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, ct) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  ── Phase {ctx.PhaseName} complete ──");
            Console.ResetColor();
            return ValueTask.CompletedTask;
        },
    });

var phases = new[] { classificationPhase, resolutionPhase, escalationPhase };

// ── Print banner ────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     Customer Support Triage — 3-Phase Pipeline              ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Demonstrates: PipelinePhase, PipelinePhasePolicy,          ║");
Console.WriteLine("║  OnEnterAsync, ShouldSkip, typed state, DelegateStageExecutor║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"📋 Ticket: \"{state.CustomerMessage}\"");
Console.WriteLine();

// ── Run phased pipeline ─────────────────────────────────────────────────
var workspace = new InMemoryWorkspace();
var runner = new SequentialPipelineRunner(diagnosticsAccessor, budgetTracker, progressFactory);

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var result = await runner.RunPhasedAsync(workspace, phases, state, options: null, CancellationToken.None);
stopwatch.Stop();

// ── Print result summary ────────────────────────────────────────────────
Console.WriteLine();
var totalStages = phases.Sum(p => p.Stages.Count);
var completedCount = result.Stages.Count(s => s.Outcome != StageOutcome.Skipped);
var skippedCount = result.Stages.Count(s => s.Outcome == StageOutcome.Skipped);

Console.ForegroundColor = result.Succeeded ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"Pipeline completed: {totalStages} stages, {phases.Length} phases, "
    + $"{(result.Succeeded ? "SUCCESS" : "FAILED")}");
Console.ResetColor();
Console.WriteLine($"  Completed: {completedCount}  Skipped: {skippedCount}  Duration: {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
Console.WriteLine();

// ── Print pipeline state summary ────────────────────────────────────────
Console.WriteLine("═══ Pipeline State Summary ═══");
Console.WriteLine($"  Intent:       {state.DetectedIntent}");
Console.WriteLine($"  Urgency:      {state.UrgencyScore}/5");
Console.WriteLine($"  Articles:     {state.ArticlesFound}");
Console.WriteLine($"  Draft:        {state.DraftWordCount} words");
Console.WriteLine($"  Fact-checked: {state.FactCheckPassed}");
Console.WriteLine($"  Supervisor:   {(state.SupervisorApproved ? "approved" : "n/a")}");
Console.WriteLine($"  Handoff:      {(state.HandoffReady ? "ready" : "n/a")}");
Console.WriteLine();

// ── Print workspace files ───────────────────────────────────────────────
Console.WriteLine("═══ Workspace Files ═══");
foreach (var file in workspace.GetFilePaths().OrderBy(f => f))
{
    var content = workspace.TryReadFile(file).Value.Content;
    Console.WriteLine($"  📄 {file} — \"{content}\"");
}
