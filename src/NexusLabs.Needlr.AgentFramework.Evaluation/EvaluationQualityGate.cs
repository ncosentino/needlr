using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Configurable quality gate that asserts evaluation metrics meet defined
/// thresholds. Designed for CI pipelines — call <see cref="Assert"/> after
/// running evaluators to fail the build when metrics regress.
/// </summary>
/// <remarks>
/// <para>
/// Thresholds are defined fluently via <see cref="RequireNumericMax"/>,
/// <see cref="RequireNumericMin"/>, and <see cref="RequireBoolean"/>. Each
/// threshold names a metric (using the <c>*MetricName</c> constants from
/// evaluator classes) and a bound. <see cref="Assert"/> checks all thresholds
/// against the evaluation result and throws
/// <see cref="QualityGateFailedException"/> when the structured decision is
/// failed or inconclusive.
/// </para>
/// <para>
/// Required missing or invalid metrics produce an inconclusive decision.
/// Use the <c>Optional*</c> methods for metrics that are conditionally emitted.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var gate = new EvaluationQualityGate()
///     .RequireBoolean(ToolCallTrajectoryEvaluator.AllSucceededMetricName, expected: true)
///     .RequireBoolean(IterationCoherenceEvaluator.TerminatedCoherentlyMetricName, expected: true)
///     .RequireNumericMax(EfficiencyEvaluator.TotalTokensMetricName, max: 50_000)
///     .RequireBoolean(EfficiencyEvaluator.UnderBudgetMetricName, expected: true);
///
/// // Throws QualityGateFailedException if any threshold is violated.
/// gate.Assert(trajectoryResult, coherenceResult, efficiencyResult);
/// </code>
/// </example>
public sealed class EvaluationQualityGate
{
    private readonly EvaluationThresholdEvaluator _thresholdEvaluator = new();

    /// <summary>
    /// Requires a <see cref="NumericMetric"/> to be at most <paramref name="max"/>.
    /// </summary>
    /// <param name="metricName">The metric name (use evaluator <c>*MetricName</c> constants).</param>
    /// <param name="max">The maximum allowed value (inclusive).</param>
    /// <returns>This gate instance for fluent chaining.</returns>
    public EvaluationQualityGate RequireNumericMax(string metricName, double max)
    {
        _thresholdEvaluator.RequireNumericMax(metricName, max);
        return this;
    }

    /// <summary>
    /// Checks a <see cref="NumericMetric"/> maximum when the metric is present.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="max">The maximum allowed value.</param>
    /// <returns>This gate instance for fluent chaining.</returns>
    public EvaluationQualityGate OptionalNumericMax(string metricName, double max)
    {
        _thresholdEvaluator.OptionalNumericMax(metricName, max);
        return this;
    }

    /// <summary>
    /// Requires a <see cref="NumericMetric"/> to be at least <paramref name="min"/>.
    /// </summary>
    /// <param name="metricName">The metric name (use evaluator <c>*MetricName</c> constants).</param>
    /// <param name="min">The minimum allowed value (inclusive).</param>
    /// <returns>This gate instance for fluent chaining.</returns>
    public EvaluationQualityGate RequireNumericMin(string metricName, double min)
    {
        _thresholdEvaluator.RequireNumericMin(metricName, min);
        return this;
    }

    /// <summary>
    /// Checks a <see cref="NumericMetric"/> minimum when the metric is present.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="min">The minimum allowed value.</param>
    /// <returns>This gate instance for fluent chaining.</returns>
    public EvaluationQualityGate OptionalNumericMin(string metricName, double min)
    {
        _thresholdEvaluator.OptionalNumericMin(metricName, min);
        return this;
    }

    /// <summary>
    /// Requires a <see cref="BooleanMetric"/> to equal <paramref name="expected"/>.
    /// </summary>
    /// <param name="metricName">The metric name (use evaluator <c>*MetricName</c> constants).</param>
    /// <param name="expected">The required boolean value.</param>
    /// <returns>This gate instance for fluent chaining.</returns>
    public EvaluationQualityGate RequireBoolean(string metricName, bool expected)
    {
        _thresholdEvaluator.RequireBoolean(metricName, expected);
        return this;
    }

    /// <summary>
    /// Checks a <see cref="BooleanMetric"/> value when the metric is present.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="expected">The required value when present.</param>
    /// <returns>This gate instance for fluent chaining.</returns>
    public EvaluationQualityGate OptionalBoolean(string metricName, bool expected)
    {
        _thresholdEvaluator.OptionalBoolean(metricName, expected);
        return this;
    }

    /// <summary>
    /// Evaluates all configured thresholds without throwing.
    /// </summary>
    /// <param name="results">One or more evaluation results to check.</param>
    /// <returns>The structured threshold result.</returns>
    public EvaluationThresholdResult Evaluate(params EvaluationResult[] results) =>
        _thresholdEvaluator.Evaluate(results);

    /// <summary>
    /// Evaluates all configured thresholds without throwing.
    /// </summary>
    /// <param name="missingMetricBehavior">
    /// The treatment for required missing or invalid metrics.
    /// </param>
    /// <param name="results">One or more evaluation results to check.</param>
    /// <returns>The structured threshold result.</returns>
    public EvaluationThresholdResult Evaluate(
        EvaluationMissingMetricBehavior missingMetricBehavior,
        params EvaluationResult[] results) =>
        _thresholdEvaluator.Evaluate(missingMetricBehavior, results);

    /// <summary>
    /// Checks all thresholds against the provided evaluation results. Metrics
    /// are looked up across all results — the first match wins.
    /// </summary>
    /// <param name="results">One or more <see cref="EvaluationResult"/> instances to check.</param>
    /// <exception cref="QualityGateFailedException">
    /// Thrown when the structured threshold decision is failed or inconclusive. The exception
    /// message lists every failed, missing, or invalid required threshold.
    /// </exception>
    public void Assert(params EvaluationResult[] results)
    {
        var evaluation = Evaluate(results);
        if (evaluation.Decision != EvaluationDecision.Passed)
        {
            var violations = evaluation.Outcomes
                .Where(outcome =>
                    outcome.Status != EvaluationThresholdStatus.Passed
                    && (outcome.IsRequired
                        || outcome.Status != EvaluationThresholdStatus.Missing))
                .Select(outcome => outcome.Message)
                .ToArray();
            throw new QualityGateFailedException(violations);
        }
    }
}
