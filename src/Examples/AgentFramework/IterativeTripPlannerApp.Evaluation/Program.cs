using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

using IterativeTripPlannerApp.Core;

using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.Copilot;

// =============================================================================
// IterativeTripPlannerApp.Evaluation
// -----------------------------------------------------------------------------
// Demonstrates end-to-end evaluation of an agent run using the
// NexusLabs.Needlr.AgentFramework.Evaluation library.
//
// Flow:
//   1. Run the trip planner via IterativeTripPlannerApp.Core
//   2. Extract diagnostics → ToEvaluationInputs() adapter
//   3. Run deterministic Needlr-native evaluators (no LLM needed)
//   4. Run MS MEAI quality evaluators (RelevanceEvaluator) with CopilotChatClient as judge
//   5. Print all metrics
//
// This app demonstrates code reuse: the same TripPlannerRunner is used by
// the main console app (IterativeTripPlannerApp) and by this evaluation app.
//
// Evaluator features showcased:
//   - ToolCallTrajectoryEvaluator: total/failed/gaps/all-succeeded (original)
//     PLUS consecutive same-tool calls, per-tool failure rate, latency P50/P95
//   - IterationCoherenceEvaluator: count/empty/coherent (original)
//     PLUS efficiency ratio, degenerate loop detection, max iterations hit
//   - TerminationAppropriatenessEvaluator: success/consistency/mode
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login` first)
// =============================================================================

PrintHeader("Needlr Agent Framework — Evaluation Demo");

// ── Configure LLM and tools ────────────────────────────────────────────
var copilotOptions = new CopilotChatClientOptions
{
    DefaultModel = "claude-sonnet-4",
};
IChatClient chatClient = new CopilotChatClient(copilotOptions);
var copilotTools = CopilotToolSet.Create(t => t.EnableWebSearch = true);

PrintSection("Configuration");
Console.WriteLine($"  Chat client:  CopilotChatClient (model: {copilotOptions.DefaultModel})");
Console.WriteLine($"  Tools:        {copilotTools.Count} (web_search)");
Console.WriteLine($"  Judge:        CopilotChatClient (same provider)");

// ── Run the trip planner via Core library ───────────────────────────────
var config = new TripPlannerConfig(
    Origin: "New York",
    Destination: "Tokyo",
    MaxStops: 5,
    MinStops: 3,
    Budget: "3000");

PrintSection($"Running trip planner: {config.Origin} → {config.Destination} (budget: ${config.Budget})");

var hooks = new TripPlannerHooks
{
    OnIterationStart = (iteration, ctx) =>
    {
        Console.WriteLine($"  ▶ Iteration {iteration}");
        return Task.CompletedTask;
    },
    OnToolCall = (iteration, tc) =>
    {
        Console.WriteLine($"    ├─ {tc.FunctionName}");
        return Task.CompletedTask;
    },
};

var runner = new TripPlannerRunner(chatClient, copilotTools);
var runResult = await runner.RunAsync(config, hooks, CancellationToken.None);

var loopResult = runResult.LoopResult;
var diagnostics = runResult.Diagnostics;

PrintSection("Run result");
Console.WriteLine($"  Succeeded:    {loopResult.Succeeded}");
Console.WriteLine($"  Termination:  {loopResult.Termination}");
Console.WriteLine($"  Iterations:   {loopResult.Iterations.Count}");
Console.WriteLine($"  Tool calls:   {loopResult.Iterations.Sum(i => i.ToolCalls.Count)}");
if (diagnostics is not null)
{
    Console.WriteLine($"  Input tokens: {diagnostics.AggregateTokenUsage.InputTokens:N0}");
    Console.WriteLine($"  Output tokens:{diagnostics.AggregateTokenUsage.OutputTokens:N0}");
}

// ── Convert diagnostics to evaluation inputs ────────────────────────────
if (diagnostics is null)
{
    PrintSection("SKIPPED — no diagnostics captured");
    Console.WriteLine("  The trip planner run did not produce diagnostics.");
    Console.WriteLine("  Evaluation requires captured diagnostics to proceed.");
    return 1;
}

PrintSection("Converting diagnostics → EvaluationInputs");
var evalInputs = diagnostics.ToEvaluationInputs();
Console.WriteLine($"  Input messages:  {evalInputs.Messages.Count}");
Console.WriteLine($"  Response text:   {(evalInputs.ModelResponse.Text?.Length > 80 ? evalInputs.ModelResponse.Text[..77] + "..." : evalInputs.ModelResponse.Text ?? "(empty)")}");

// ── Run Needlr-native deterministic evaluators ──────────────────────────
PrintSection("Native evaluators (deterministic — no LLM required)");

var diagContext = new AgentRunDiagnosticsContext(diagnostics);

var trajectoryEval = new ToolCallTrajectoryEvaluator();
var trajectoryResult = await trajectoryEval.EvaluateAsync(
    evalInputs.Messages,
    evalInputs.ModelResponse,
    additionalContext: [diagContext]);
PrintMetrics("ToolCallTrajectoryEvaluator", trajectoryResult);

var terminationEval = new TerminationAppropriatenessEvaluator();
var terminationResult = await terminationEval.EvaluateAsync(
    evalInputs.Messages,
    evalInputs.ModelResponse,
    additionalContext: [diagContext]);
PrintMetrics("TerminationAppropriatenessEvaluator", terminationResult);

var coherenceEval = new IterationCoherenceEvaluator(maxIterations: 20);
var coherenceResult = await coherenceEval.EvaluateAsync(
    evalInputs.Messages,
    evalInputs.ModelResponse,
    additionalContext: [diagContext]);
PrintMetrics("IterationCoherenceEvaluator", coherenceResult);

var efficiencyEval = new EfficiencyEvaluator(tokenBudget: 200_000);
var efficiencyResult = await efficiencyEval.EvaluateAsync(
    evalInputs.Messages,
    evalInputs.ModelResponse,
    additionalContext: [diagContext]);
PrintMetrics("EfficiencyEvaluator", efficiencyResult);

// ── Quality gate — CI regression detection ──────────────────────────────
PrintSection("Quality gate (CI regression detection)");

var gate = new EvaluationQualityGate()
    .RequireBoolean(ToolCallTrajectoryEvaluator.AllSucceededMetricName, expected: true)
    .RequireBoolean(IterationCoherenceEvaluator.TerminatedCoherentlyMetricName, expected: true)
    .RequireNumericMax(EfficiencyEvaluator.TotalTokensMetricName, max: 200_000)
    .RequireBoolean(EfficiencyEvaluator.UnderBudgetMetricName, expected: true)
    .RequireNumericMin(IterationCoherenceEvaluator.EfficiencyRatioMetricName, min: 0.5);

try
{
    gate.Assert(trajectoryResult, terminationResult, coherenceResult, efficiencyResult);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  [PASS] All quality gate thresholds met.");
    Console.ResetColor();
}
catch (QualityGateFailedException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [FAIL] {ex.Message}");
    Console.ResetColor();
}

// ── Run MS MEAI quality evaluator (requires LLM judge) ──────────────────
PrintSection("MS MEAI evaluators (LLM-judged via CopilotChatClient)");

IChatClient judge = new CopilotChatClient(new CopilotChatClientOptions
{
    DefaultModel = "claude-sonnet-4",
});
var chatConfiguration = new ChatConfiguration(judge);

var relevanceEval = new RelevanceEvaluator();
try
{
    var relevanceResult = await relevanceEval.EvaluateAsync(
        evalInputs.Messages,
        evalInputs.ModelResponse,
        chatConfiguration);
    PrintMetrics("RelevanceEvaluator", relevanceResult);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  [FAILED] RelevanceEvaluator: {ex.GetType().Name}: {ex.Message}");
    Console.ResetColor();
}

// ── Run Needlr LLM-judged evaluator ─────────────────────────────────────
PrintSection("Needlr LLM-judged evaluator (TaskCompletionEvaluator)");

var taskCompletionEval = new TaskCompletionEvaluator();
try
{
    var taskResult = await taskCompletionEval.EvaluateAsync(
        evalInputs.Messages.Count > 0
            ? evalInputs.Messages
            : [new ChatMessage(ChatRole.User, $"Plan a trip from {config.Origin} to {config.Destination} with {config.MinStops}-{config.MaxStops} stops on a ${config.Budget} budget.")],
        evalInputs.ModelResponse,
        chatConfiguration,
        additionalContext: [diagContext],
        cancellationToken: CancellationToken.None);
    PrintMetrics("TaskCompletionEvaluator", taskResult);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  [FAILED] TaskCompletionEvaluator: {ex.GetType().Name}: {ex.Message}");
    Console.ResetColor();
}

// ── Summary ─────────────────────────────────────────────────────────────
PrintHeader("Done");
Console.WriteLine();
Console.WriteLine("  This demo showed:");
Console.WriteLine("  1. Running a real agent scenario via IterativeTripPlannerApp.Core");
Console.WriteLine("  2. Extracting diagnostics → EvaluationInputs via ToEvaluationInputs()");
Console.WriteLine("  3. Scoring with deterministic Needlr-native evaluators:");
Console.WriteLine("     - ToolCallTrajectoryEvaluator: total, failed, gaps, all-succeeded,");
Console.WriteLine("       consecutive same-tool, per-tool failure rate, latency P50/P95");
Console.WriteLine("     - IterationCoherenceEvaluator: count, empty outputs, coherent,");
Console.WriteLine("       efficiency ratio, degenerate loop detection, max iterations hit");
Console.WriteLine("     - TerminationAppropriatenessEvaluator: success, consistency, mode");
Console.WriteLine("     - EfficiencyEvaluator: total tokens, input ratio, tokens/tool-call,");
Console.WriteLine("       cache hit ratio, under-budget check");
Console.WriteLine("  4. Asserting quality gates for CI regression detection");
Console.WriteLine("  5. Scoring with MS MEAI quality evaluators using Copilot as judge");
Console.WriteLine("  6. Scoring with Needlr LLM-judged TaskCompletionEvaluator");
Console.WriteLine();

return 0;

static void PrintMetrics(string evaluatorName, EvaluationResult result)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  [OK] {evaluatorName} — {result.Metrics.Count} metric(s):");
    Console.ResetColor();
    foreach (var kvp in result.Metrics)
    {
        var metric = kvp.Value;
        var value = metric switch
        {
            NumericMetric nm => nm.Value?.ToString("F2") ?? "n/a",
            BooleanMetric bm => bm.Value?.ToString() ?? "n/a",
            StringMetric sm => sm.Value?.Length > 60 ? sm.Value[..57] + "..." : sm.Value ?? "n/a",
            _ => metric.Interpretation?.Rating.ToString() ?? "n/a",
        };
        Console.WriteLine($"    • {metric.Name} = {value}");
        if (metric.Reason is not null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      └─ {metric.Reason}");
            Console.ResetColor();
        }
    }
}

static void PrintHeader(string text)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(new string('=', 78));
    Console.WriteLine($" {text}");
    Console.WriteLine(new string('=', 78));
    Console.ResetColor();
}

static void PrintSection(string text)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"-- {text} " + new string('-', Math.Max(0, 74 - text.Length)));
    Console.ResetColor();
}
