using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Deterministic evaluator that scores whether an agent run terminated appropriately,
/// using the captured <see cref="IAgentRunDiagnostics"/> snapshot carried in an
/// <see cref="AgentRunDiagnosticsContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// When the <see cref="AgentRunDiagnosticsContext"/> is present, the evaluator emits:
/// </para>
/// <list type="bullet">
///   <item><description><c>Run Succeeded</c> — boolean; mirrors <see cref="IAgentRunDiagnostics.Succeeded"/>.</description></item>
///   <item><description><c>Termination Consistent</c> — boolean; <see langword="true"/> when <c>Succeeded</c> is consistent with <see cref="IAgentRunDiagnostics.ErrorMessage"/> (success ⇔ no error message).</description></item>
///   <item><description><c>Execution Mode</c> — string; mirrors <see cref="IAgentRunDiagnostics.ExecutionMode"/>, or <c>"Unknown"</c> when not set.</description></item>
/// </list>
/// <para>
/// When no <see cref="AgentRunDiagnosticsContext"/> is present, the evaluator returns
/// an empty <see cref="EvaluationResult"/>.
/// </para>
/// </remarks>
public sealed class TerminationAppropriatenessEvaluator : IEvaluator
{
    /// <summary>Metric name for the success rollup.</summary>
    public const string RunSucceededMetricName = "Run Succeeded";

    /// <summary>Metric name for the success/error consistency check.</summary>
    public const string TerminationConsistentMetricName = "Termination Consistent";

    /// <summary>Metric name for the captured execution mode string.</summary>
    public const string ExecutionModeMetricName = "Execution Mode";

    /// <summary>Execution mode string emitted when the diagnostics do not carry one.</summary>
    public const string UnknownExecutionMode = "Unknown";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        RunSucceededMetricName,
        TerminationConsistentMetricName,
        ExecutionModeMetricName,
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

        var runSucceeded = diagnostics.Succeeded;
        var hasErrorMessage = !string.IsNullOrEmpty(diagnostics.ErrorMessage);
        var terminationConsistent = runSucceeded != hasErrorMessage;

        var runSucceededMetric = new BooleanMetric(
            RunSucceededMetricName,
            value: runSucceeded,
            reason: runSucceeded
                ? "The agent run reported success."
                : $"The agent run failed: {diagnostics.ErrorMessage ?? "no error message captured"}.");

        var terminationConsistentMetric = new BooleanMetric(
            TerminationConsistentMetricName,
            value: terminationConsistent,
            reason: terminationConsistent
                ? "Success flag is consistent with the presence/absence of an error message."
                : runSucceeded
                    ? "The run reported success but an error message was also captured."
                    : "The run reported failure but no error message was captured.");

        var executionMode = string.IsNullOrEmpty(diagnostics.ExecutionMode)
            ? UnknownExecutionMode
            : diagnostics.ExecutionMode!;

        var executionModeMetric = new StringMetric(
            ExecutionModeMetricName,
            value: executionMode,
            reason: $"The captured execution mode was '{executionMode}'.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            runSucceededMetric,
            terminationConsistentMetric,
            executionModeMetric));
    }
}
