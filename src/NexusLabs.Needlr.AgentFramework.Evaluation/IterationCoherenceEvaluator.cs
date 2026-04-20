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
///   <item><description><c>Iteration Efficiency Ratio</c> — ratio of useful iterations (produced text output or triggered tool calls) to total iterations.</description></item>
///   <item><description><c>Degenerate Loop Detected</c> — boolean. <see langword="true"/> when two or more consecutive iterations produced identical text output. Requires <see cref="ChatCompletionDiagnostics.Response"/> to be captured; defaults to <see langword="false"/> when response data is unavailable.</description></item>
///   <item><description><c>Max Iterations Hit</c> — boolean. <see langword="true"/> when the iteration count reached or exceeded the configured <c>maxIterations</c> threshold. Only emitted when <c>maxIterations</c> was provided to the constructor.</description></item>
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

    /// <summary>Metric name for the ratio of useful iterations to total iterations.</summary>
    public const string EfficiencyRatioMetricName = "Iteration Efficiency Ratio";

    /// <summary>Metric name for the boolean indicating a degenerate (repeated-output) loop.</summary>
    public const string DegenerateLoopMetricName = "Degenerate Loop Detected";

    /// <summary>Metric name for the boolean indicating the iteration count reached maxIterations.</summary>
    public const string MaxIterationsHitMetricName = "Max Iterations Hit";

    private readonly int? _maxIterations;

    /// <summary>
    /// Creates a new <see cref="IterationCoherenceEvaluator"/>.
    /// </summary>
    /// <param name="maxIterations">
    /// Optional expected iteration limit. When provided, the evaluator emits the
    /// <see cref="MaxIterationsHitMetricName"/> metric. When <see langword="null"/>,
    /// the metric is omitted.
    /// </param>
    public IterationCoherenceEvaluator(int? maxIterations = null)
    {
        _maxIterations = maxIterations;

        var names = new List<string>
        {
            IterationCountMetricName,
            EmptyOutputsMetricName,
            TerminatedCoherentlyMetricName,
            EfficiencyRatioMetricName,
            DegenerateLoopMetricName,
        };
        if (maxIterations.HasValue)
        {
            names.Add(MaxIterationsHitMetricName);
        }
        EvaluationMetricNames = names;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; }

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
        var usefulIterations = 0;
        for (var i = 0; i < completions.Count; i++)
        {
            var hasTextOutput = completions[i].ResponseCharCount > 0;
            var hasFunctionCalls = completions[i].Response?.Messages
                .Any(m => m.Contents.OfType<FunctionCallContent>().Any()) ?? false;

            if (!hasTextOutput)
            {
                emptyOutputs++;
            }

            if (hasTextOutput || hasFunctionCalls)
            {
                usefulIterations++;
            }
        }

        var finalIterationProducedOutput =
            iterationCount > 0 && completions[iterationCount - 1].ResponseCharCount > 0;
        var terminatedCoherently =
            diagnostics.Succeeded &&
            iterationCount > 0 &&
            finalIterationProducedOutput;
        var efficiencyRatio = iterationCount > 0
            ? (double)usefulIterations / iterationCount
            : 0;
        var degenerateLoop = DetectDegenerateLoop(completions);

        var metrics = new List<EvaluationMetric>
        {
            new NumericMetric(
                IterationCountMetricName,
                value: iterationCount,
                reason: iterationCount == 0
                    ? "No iterations were recorded."
                    : $"{iterationCount} iteration(s) were recorded."),

            new NumericMetric(
                EmptyOutputsMetricName,
                value: emptyOutputs,
                reason: emptyOutputs == 0
                    ? "Every iteration produced non-empty output."
                    : $"{emptyOutputs} of {iterationCount} iteration(s) produced empty output."),

            new BooleanMetric(
                TerminatedCoherentlyMetricName,
                value: terminatedCoherently,
                reason: terminatedCoherently
                    ? "The iterative loop succeeded and the final iteration produced output."
                    : BuildIncoherentReason(diagnostics, iterationCount, finalIterationProducedOutput)),

            new NumericMetric(
                EfficiencyRatioMetricName,
                value: efficiencyRatio,
                reason: iterationCount == 0
                    ? "No iterations to compute efficiency."
                    : $"{usefulIterations} of {iterationCount} iteration(s) were useful (produced text or triggered tool calls)."),

            new BooleanMetric(
                DegenerateLoopMetricName,
                value: degenerateLoop,
                reason: degenerateLoop
                    ? "Two or more consecutive iterations produced identical text output."
                    : "No consecutive duplicate outputs detected."),
        };

        if (_maxIterations.HasValue)
        {
            var hit = iterationCount >= _maxIterations.Value;
            metrics.Add(new BooleanMetric(
                MaxIterationsHitMetricName,
                value: hit,
                reason: hit
                    ? $"Iteration count ({iterationCount}) reached or exceeded the configured limit ({_maxIterations.Value})."
                    : $"Iteration count ({iterationCount}) is below the configured limit ({_maxIterations.Value})."));
        }

        return new ValueTask<EvaluationResult>(new EvaluationResult(metrics.ToArray()));
    }

    private static bool DetectDegenerateLoop(IReadOnlyList<ChatCompletionDiagnostics> completions)
    {
        if (completions.Count < 2)
        {
            return false;
        }

        for (var i = 1; i < completions.Count; i++)
        {
            var prevResponse = completions[i - 1].Response;
            var currResponse = completions[i].Response;

            if (prevResponse is null || currResponse is null)
            {
                continue;
            }

            var prevText = GetAggregateText(prevResponse);
            var currText = GetAggregateText(currResponse);

            if (prevText is not null &&
                currText is not null &&
                string.Equals(prevText, currText, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetAggregateText(ChatResponse response)
    {
        if (response.Messages.Count == 0)
        {
            return null;
        }

        var text = response.Messages[response.Messages.Count - 1].Text;
        return string.IsNullOrEmpty(text) ? null : text;
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
