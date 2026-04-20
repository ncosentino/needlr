using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Deterministic evaluator that scores the tool-call trajectory of an agent run from
/// the captured <see cref="IAgentRunDiagnostics"/> snapshot carried in an
/// <see cref="AgentRunDiagnosticsContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This evaluator never contacts a language model. It reads the ordered
/// <see cref="IAgentRunDiagnostics.ToolCalls"/> collection and produces:
/// </para>
/// <list type="bullet">
///   <item><description><c>Tool Calls Total</c> — total number of tool invocations.</description></item>
///   <item><description><c>Tool Calls Failed</c> — count of tool invocations whose <see cref="ToolCallDiagnostics.Succeeded"/> is <see langword="false"/>.</description></item>
///   <item><description><c>Tool Call Sequence Gaps</c> — number of missing slots in the <see cref="ToolCallDiagnostics.Sequence"/> stream (a strictly increasing sequence starting at <c>0</c> has zero gaps).</description></item>
///   <item><description><c>All Tool Calls Succeeded</c> — boolean rollup. <see langword="true"/> when every tool invocation succeeded (or when no tool calls occurred).</description></item>
///   <item><description><c>Consecutive Same-Tool Calls</c> — count of consecutive tool invocations with the same <see cref="ToolCallDiagnostics.ToolName"/>. Useful as a heuristic for stuck or looping agents. Note: parallel fan-out to the same tool (valid usage) will also increment this counter.</description></item>
///   <item><description><c>Per-Tool Failure Rate</c> — JSON string mapping each tool name to its failure rate (0.0–1.0), sorted alphabetically.</description></item>
///   <item><description><c>Tool Call Latency P50</c> — 50th percentile of tool call durations in milliseconds (nearest-rank method).</description></item>
///   <item><description><c>Tool Call Latency P95</c> — 95th percentile of tool call durations in milliseconds (nearest-rank method).</description></item>
/// </list>
/// <para>
/// When no <see cref="AgentRunDiagnosticsContext"/> is present in the
/// <c>additionalContext</c> collection, the evaluator returns an empty
/// <see cref="EvaluationResult"/> — callers should treat that as "not applicable".
/// </para>
/// </remarks>
public sealed class ToolCallTrajectoryEvaluator : IEvaluator
{
    /// <summary>Metric name for the total tool-call count.</summary>
    public const string TotalMetricName = "Tool Calls Total";

    /// <summary>Metric name for the failed tool-call count.</summary>
    public const string FailedMetricName = "Tool Calls Failed";

    /// <summary>Metric name for the number of gaps in the recorded tool-call sequence.</summary>
    public const string SequenceGapsMetricName = "Tool Call Sequence Gaps";

    /// <summary>Metric name for the boolean rollup indicating every tool call succeeded.</summary>
    public const string AllSucceededMetricName = "All Tool Calls Succeeded";

    /// <summary>Metric name for the count of consecutive tool calls with the same tool name.</summary>
    public const string ConsecutiveSameToolMetricName = "Consecutive Same-Tool Calls";

    /// <summary>Metric name for the JSON-formatted per-tool failure rate breakdown.</summary>
    public const string PerToolFailureRateMetricName = "Per-Tool Failure Rate";

    /// <summary>Metric name for the 50th percentile tool-call latency in milliseconds.</summary>
    public const string LatencyP50MetricName = "Tool Call Latency P50";

    /// <summary>Metric name for the 95th percentile tool-call latency in milliseconds.</summary>
    public const string LatencyP95MetricName = "Tool Call Latency P95";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        TotalMetricName,
        FailedMetricName,
        SequenceGapsMetricName,
        AllSucceededMetricName,
        ConsecutiveSameToolMetricName,
        PerToolFailureRateMetricName,
        LatencyP50MetricName,
        LatencyP95MetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = additionalContext?
            .OfType<AgentRunDiagnosticsContext>()
            .FirstOrDefault()?
            .Diagnostics;

        if (diagnostics is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var toolCalls = diagnostics.ToolCalls;
        var total = toolCalls.Count;
        var failed = 0;
        for (var i = 0; i < toolCalls.Count; i++)
        {
            if (!toolCalls[i].Succeeded)
            {
                failed++;
            }
        }

        var gaps = CountSequenceGaps(toolCalls);
        var allSucceeded = failed == 0;
        var consecutiveSameTool = CountConsecutiveSameTool(toolCalls);
        var perToolFailureRate = BuildPerToolFailureRate(toolCalls);
        var (p50, p95) = ComputeLatencyPercentiles(toolCalls);

        var totalMetric = new NumericMetric(
            TotalMetricName,
            value: total,
            reason: total == 0
                ? "No tool calls were recorded for this agent run."
                : $"{total} tool call(s) were recorded.");

        var failedMetric = new NumericMetric(
            FailedMetricName,
            value: failed,
            reason: failed == 0
                ? "All recorded tool calls succeeded."
                : $"{failed} of {total} recorded tool call(s) failed.");

        var gapsMetric = new NumericMetric(
            SequenceGapsMetricName,
            value: gaps,
            reason: gaps == 0
                ? "The tool-call sequence is contiguous starting at 0."
                : $"{gaps} gap(s) detected in the tool-call sequence.");

        var allSucceededMetric = new BooleanMetric(
            AllSucceededMetricName,
            value: allSucceeded,
            reason: allSucceeded
                ? "Every recorded tool call reported success."
                : "At least one recorded tool call reported failure.");

        var consecutiveMetric = new NumericMetric(
            ConsecutiveSameToolMetricName,
            value: consecutiveSameTool,
            reason: consecutiveSameTool == 0
                ? "No consecutive same-tool calls detected."
                : $"{consecutiveSameTool} consecutive same-tool call(s) detected (heuristic — may include valid parallel fan-out).");

        var failureRateMetric = new StringMetric(
            PerToolFailureRateMetricName,
            value: perToolFailureRate,
            reason: total == 0
                ? "No tool calls to compute failure rates."
                : "Per-tool failure rates as JSON (tool name → failure rate 0.0–1.0).");

        var p50Metric = new NumericMetric(
            LatencyP50MetricName,
            value: p50,
            reason: total == 0
                ? "No tool calls to compute latency."
                : $"50th percentile tool-call latency: {p50:F1}ms.");

        var p95Metric = new NumericMetric(
            LatencyP95MetricName,
            value: p95,
            reason: total == 0
                ? "No tool calls to compute latency."
                : $"95th percentile tool-call latency: {p95:F1}ms.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            totalMetric,
            failedMetric,
            gapsMetric,
            allSucceededMetric,
            consecutiveMetric,
            failureRateMetric,
            p50Metric,
            p95Metric));
    }

    private static int CountSequenceGaps(IReadOnlyList<ToolCallDiagnostics> toolCalls)
    {
        if (toolCalls.Count == 0)
        {
            return 0;
        }

        var sequences = new int[toolCalls.Count];
        for (var i = 0; i < toolCalls.Count; i++)
        {
            sequences[i] = toolCalls[i].Sequence;
        }
        Array.Sort(sequences);

        var gaps = 0;
        var expected = sequences[0];
        for (var i = 0; i < sequences.Length; i++)
        {
            var actual = sequences[i];
            if (actual > expected)
            {
                gaps += actual - expected;
            }
            expected = actual + 1;
        }

        return gaps;
    }

    private static int CountConsecutiveSameTool(IReadOnlyList<ToolCallDiagnostics> toolCalls)
    {
        if (toolCalls.Count <= 1)
        {
            return 0;
        }

        var count = 0;
        for (var i = 1; i < toolCalls.Count; i++)
        {
            if (string.Equals(toolCalls[i].ToolName, toolCalls[i - 1].ToolName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static string BuildPerToolFailureRate(IReadOnlyList<ToolCallDiagnostics> toolCalls)
    {
        if (toolCalls.Count == 0)
        {
            return "{}";
        }

        var totals = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var failures = new SortedDictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < toolCalls.Count; i++)
        {
            var name = toolCalls[i].ToolName;
            totals.TryGetValue(name, out var t);
            totals[name] = t + 1;

            if (!toolCalls[i].Succeeded)
            {
                failures.TryGetValue(name, out var f);
                failures[name] = f + 1;
            }
        }

        var rates = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var kvp in totals)
        {
            failures.TryGetValue(kvp.Key, out var f);
            rates[kvp.Key] = (double)f / kvp.Value;
        }

        return JsonSerializer.Serialize(rates);
    }

    private static (double P50, double P95) ComputeLatencyPercentiles(
        IReadOnlyList<ToolCallDiagnostics> toolCalls)
    {
        if (toolCalls.Count == 0)
        {
            return (0, 0);
        }

        var durations = new double[toolCalls.Count];
        for (var i = 0; i < toolCalls.Count; i++)
        {
            durations[i] = toolCalls[i].Duration.TotalMilliseconds;
        }
        Array.Sort(durations);

        return (NearestRankPercentile(durations, 50), NearestRankPercentile(durations, 95));
    }

    private static double NearestRankPercentile(double[] sorted, int percentile)
    {
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
