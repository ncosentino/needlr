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
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using CodeReviewPipelineApp;

// ============================================================================
// Code Review Pipeline — Phase-Level Token Budgets
//
// Demonstrates how PipelinePhase and PipelinePhasePolicy enforce per-phase
// token budget boundaries. Each phase gets an independent budget scope, and
// lifecycle hooks (OnEnterAsync / OnExitAsync) log allocation and consumption.
//
// All stages use DelegateStageExecutor with simulated logic — no LLM calls,
// no API keys required. The example runs instantly.
// ============================================================================

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// ── Needlr DI setup ────────────────────────────────────────────────────
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingDiagnostics()
        .UsingTokenBudget())
    .UsingPostPluginRegistrationCallback(services =>
    {
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Warning));
    })
    .BuildServiceProvider(configuration);

var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();
var budgetTracker = serviceProvider.GetRequiredService<ITokenBudgetTracker>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();

// ── Pipeline state ──────────────────────────────────────────────────────
var state = new CodeReviewState();

// ── Define per-phase token budgets ──────────────────────────────────────
const long analysisBudget = 10_000;
const long synthesisBudget = 30_000;
const long reportingBudget = 5_000;

// ── Phase 1: Analysis — lightweight parsing and pattern detection ───────
var analysisPhase = new PipelinePhase(
    "Analysis",
    [
        new PipelineStage("ParseDiff", new DelegateStageExecutor(async (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            reviewState.HunkCount = 3;
            reviewState.FileCount = 2;
            ctx.Workspace.TryWriteFile("diff.patch", "@@ -12,6 +12,10 @@ public class TokenValidator\n+    private readonly ILogger _logger;\n+    public bool ValidateToken(string token) => token != null;");
        })),
        new PipelineStage("DetectPatterns", new DelegateStageExecutor(async (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            reviewState.AntiPatterns.Add("Null check via != instead of pattern matching");
            reviewState.AntiPatterns.Add("Missing input validation on public API");
        })),
        new PipelineStage("SecurityScan", new DelegateStageExecutor(async (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            reviewState.SecurityFindings.Add("[MEDIUM] Token comparison susceptible to timing attacks — use constant-time comparison");
        })),
    ],
    new PipelinePhasePolicy
    {
        TokenBudget = analysisBudget,
        OnEnterAsync = (ctx, ct) =>
        {
            Console.WriteLine($"🔵 Phase {ctx.PhaseIndex + 1}: {ctx.PhaseName} [Budget: {analysisBudget:N0} tokens]");
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            Console.WriteLine($"  ✓ ParseDiff — {reviewState.HunkCount} hunks, {reviewState.FileCount} files");
            Console.WriteLine($"  ✓ DetectPatterns — found {reviewState.AntiPatterns.Count} patterns");
            Console.WriteLine($"  ✓ SecurityScan — {reviewState.SecurityFindings.Count} finding (medium)");
            Console.WriteLine($"  Budget: 0 / {analysisBudget:N0} consumed (simulated)");
            Console.WriteLine();
            return ValueTask.CompletedTask;
        },
    });

// ── Phase 2: Synthesis — higher budget for comment generation ───────────
var synthesisPhase = new PipelinePhase(
    "Synthesis",
    [
        new PipelineStage("GenerateComments", new DelegateStageExecutor(async (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            reviewState.ReviewComments.Add("Line 14: Use `is not null` pattern matching instead of `!= null`");
            reviewState.ReviewComments.Add("Line 14: ValidateToken should throw ArgumentNullException for null input");
            reviewState.ReviewComments.Add("Line 14: Consider using CryptographicOperations.FixedTimeEquals for token comparison");
            reviewState.ReviewComments.Add("Line 12: _logger field is declared but never used — wire up ILogger via constructor injection");
        })),
        new PipelineStage("PrioritizeFindings", new DelegateStageExecutor(async (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            reviewState.FindingsPrioritized = true;
        })),
    ],
    new PipelinePhasePolicy
    {
        TokenBudget = synthesisBudget,
        OnEnterAsync = (ctx, ct) =>
        {
            Console.WriteLine($"🟡 Phase {ctx.PhaseIndex + 1}: {ctx.PhaseName} [Budget: {synthesisBudget:N0} tokens]");
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            Console.WriteLine($"  ✓ GenerateComments — {reviewState.ReviewComments.Count} review comments");
            Console.WriteLine($"  ✓ PrioritizeFindings — ranked by severity");
            Console.WriteLine($"  Budget: 0 / {synthesisBudget:N0} consumed (simulated)");
            Console.WriteLine();
            return ValueTask.CompletedTask;
        },
    });

// ── Phase 3: Reporting — minimal budget for formatting ──────────────────
var reportingPhase = new PipelinePhase(
    "Reporting",
    [
        new PipelineStage("FormatPRComment", new DelegateStageExecutor(async (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            var comment = "## Code Review\n\n";
            comment += "### Findings\n";
            foreach (var c in reviewState.ReviewComments)
            {
                comment += $"- {c}\n";
            }
            comment += "\n### Security\n";
            foreach (var s in reviewState.SecurityFindings)
            {
                comment += $"- {s}\n";
            }
            reviewState.PrComment = comment;
            ctx.Workspace.TryWriteFile("pr-comment.md", comment);
        })),
        new PipelineStage("GenerateSummary", new DelegateStageExecutor(async (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            reviewState.Summary = $"Review of {reviewState.FileCount} files found {reviewState.AntiPatterns.Count} anti-patterns and {reviewState.SecurityFindings.Count} security issue. {reviewState.ReviewComments.Count} comments generated.";
            ctx.Workspace.TryWriteFile("summary.txt", reviewState.Summary);
        })),
    ],
    new PipelinePhasePolicy
    {
        TokenBudget = reportingBudget,
        OnEnterAsync = (ctx, ct) =>
        {
            Console.WriteLine($"🟢 Phase {ctx.PhaseIndex + 1}: {ctx.PhaseName} [Budget: {reportingBudget:N0} tokens]");
            return ValueTask.CompletedTask;
        },
        OnExitAsync = (ctx, ct) =>
        {
            var reviewState = ctx.GetRequiredState<CodeReviewState>();
            Console.WriteLine($"  ✓ FormatPRComment — PR comment ready ({reviewState.PrComment.Length} chars)");
            Console.WriteLine($"  ✓ GenerateSummary — summary ready");
            Console.WriteLine($"  Budget: 0 / {reportingBudget:N0} consumed (simulated)");
            Console.WriteLine();
            return ValueTask.CompletedTask;
        },
    });

// ── Assemble phases ─────────────────────────────────────────────────────
var phases = new[] { analysisPhase, synthesisPhase, reportingPhase };
var totalStages = phases.Sum(p => p.Stages.Count);
var totalBudget = analysisBudget + synthesisBudget + reportingBudget;

// ── Print banner ────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     Code Review Pipeline — Phase-Level Token Budgets        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("📋 Reviewing: src/Auth/TokenValidator.cs (+47, -12)");
Console.WriteLine();

// ── Run phased pipeline ─────────────────────────────────────────────────
var workspace = new InMemoryWorkspace();
var runner = new SequentialPipelineRunner(diagnosticsAccessor, budgetTracker, progressFactory);

var result = await runner.RunPhasedAsync(
    workspace,
    phases,
    state,
    options: null,
    CancellationToken.None);

// ── Print result ────────────────────────────────────────────────────────
Console.ForegroundColor = result.Succeeded ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"Pipeline completed: {totalStages} stages, {phases.Length} phases, {(result.Succeeded ? "SUCCESS" : "FAILED")}");
Console.ResetColor();
Console.WriteLine($"Total budget allocated: {totalBudget:N0} tokens across {phases.Length} phases");
