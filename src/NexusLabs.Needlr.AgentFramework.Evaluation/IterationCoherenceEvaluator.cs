using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Deterministic evaluator that scores the iteration coherence of an iterative-loop
/// agent run from the captured <see cref="IAgentRunDiagnostics"/> snapshot carried in
/// an <see cref="AgentRunDiagnosticsContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This evaluator only produces metrics when
/// <see cref="IAgentRunDiagnostics.ExecutionMode"/> is <c>"IterativeLoop"</c>. For any
/// other execution mode (or when the context is missing) the evaluator returns an
/// empty <see cref="EvaluationResult"/>, which callers should treat as "not applicable".
/// </para>
/// <para>
/// When applicable, the evaluator emits:
/// </para>
/// <list type="bullet">
///   <item><description><c>Iteration Count</c> — number of LLM iterations, derived from <see cref="IAgentRunDiagnostics.ChatCompletions"/>.</description></item>
///   <item><description><c>Iteration Empty Outputs</c> — number of iterations whose <see cref="ChatCompletionDiagnostics.ResponseCharCount"/> is <c>0</c>.</description></item>
///   <item><description><c>Terminated Coherently</c> — boolean rollup. <see langword="true"/> when the run succeeded, produced at least one iteration, and the final iteration produced non-empty output.</description></item>
/// </list>
/// </remarks>
public sealed class IterationCoherenceEvaluator : IEvaluator
{
    /// <summary>The execution mode value that gates this evaluator.</summary>
    public const string IterativeLoopExecutionMode = "IterativeLoop";

    /// <summary>Metric name for the iteration count.</summary>
    public const string IterationCountMetricName = "Iteration Count";

    /// <summary>Metric name for the count of iterations with empty output.</summary>
    public const string EmptyOutputsMetricName = "Iteration Empty Outputs";

    /// <summary>Metric name for the boolean rollup indicating coherent termination.</summary>
    public const string TerminatedCoherentlyMetricName = "Terminated Coherently";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        IterationCountMetricName,
        EmptyOutputsMetricName,
        TerminatedCoherentlyMetricName,
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

        if (diagnostics is null ||
            !string.Equals(diagnostics.ExecutionMode, IterativeLoopExecutionMode, StringComparison.Ordinal))
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var completions = diagnostics.ChatCompletions;
        var iterationCount = completions.Count;
        var emptyOutputs = 0;
        for (var i = 0; i < completions.Count; i++)
        {
            if (completions[i].ResponseCharCount == 0)
            {
                emptyOutputs++;
            }
        }

        var finalIterationProducedOutput =
            iterationCount > 0 && completions[iterationCount - 1].ResponseCharCount > 0;
        var terminatedCoherently =
            diagnostics.Succeeded &&
            iterationCount > 0 &&
            finalIterationProducedOutput;

        var iterationCountMetric = new NumericMetric(
            IterationCountMetricName,
            value: iterationCount,
            reason: iterationCount == 0
                ? "No iterations were recorded."
                : $"{iterationCount} iteration(s) were recorded.");

        var emptyOutputsMetric = new NumericMetric(
            EmptyOutputsMetricName,
            value: emptyOutputs,
            reason: emptyOutputs == 0
                ? "Every iteration produced non-empty output."
                : $"{emptyOutputs} of {iterationCount} iteration(s) produced empty output.");

        var terminatedCoherentlyMetric = new BooleanMetric(
            TerminatedCoherentlyMetricName,
            value: terminatedCoherently,
            reason: terminatedCoherently
                ? "The iterative loop succeeded and the final iteration produced output."
                : BuildIncoherentReason(diagnostics, iterationCount, finalIterationProducedOutput));

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            iterationCountMetric,
            emptyOutputsMetric,
            terminatedCoherentlyMetric));
    }

    private static string BuildIncoherentReason(
        IAgentRunDiagnostics diagnostics,
        int iterationCount,
        bool finalIterationProducedOutput)
    {
        if (!diagnostics.Succeeded)
        {
            return "The agent run did not complete successfully.";
        }
        if (iterationCount == 0)
        {
            return "The agent run succeeded but recorded zero iterations.";
        }
        if (!finalIterationProducedOutput)
        {
            return "The final iteration produced no output.";
        }
        return "Iterative-loop termination is incoherent.";
    }
}
