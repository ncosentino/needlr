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

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        TotalMetricName,
        FailedMetricName,
        SequenceGapsMetricName,
        AllSucceededMetricName,
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

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            totalMetric,
            failedMetric,
            gapsMetric,
            allSucceededMetric));
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
}
