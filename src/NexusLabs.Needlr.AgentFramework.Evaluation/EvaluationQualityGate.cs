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
/// <see cref="QualityGateFailedException"/> listing every violation.
/// </para>
/// <para>
/// Metrics not present in the <see cref="EvaluationResult"/> are silently
/// skipped — this allows a gate to be used with evaluators that conditionally
/// emit metrics (e.g., <see cref="IterationCoherenceEvaluator"/> only emits
/// when execution mode is <c>IterativeLoop</c>).
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
    private readonly List<Threshold> _thresholds = [];

    /// <summary>
    /// Requires a <see cref="NumericMetric"/> to be at most <paramref name="max"/>.
    /// </summary>
    /// <param name="metricName">The metric name (use evaluator <c>*MetricName</c> constants).</param>
    /// <param name="max">The maximum allowed value (inclusive).</param>
    /// <returns>This gate instance for fluent chaining.</returns>
    public EvaluationQualityGate RequireNumericMax(string metricName, double max)
    {
        _thresholds.Add(new NumericMaxThreshold(metricName, max));
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
        _thresholds.Add(new NumericMinThreshold(metricName, min));
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
        _thresholds.Add(new BooleanThreshold(metricName, expected));
        return this;
    }

    /// <summary>
    /// Checks all thresholds against the provided evaluation results. Metrics
    /// are looked up across all results — the first match wins.
    /// </summary>
    /// <param name="results">One or more <see cref="EvaluationResult"/> instances to check.</param>
    /// <exception cref="QualityGateFailedException">
    /// Thrown when one or more thresholds are violated. The exception message
    /// lists every violation.
    /// </exception>
    public void Assert(params EvaluationResult[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var violations = new List<string>();
        foreach (var threshold in _thresholds)
        {
            EvaluationMetric? metric = null;
            foreach (var result in results)
            {
                if (result.Metrics.TryGetValue(threshold.MetricName, out var found))
                {
                    metric = found;
                    break;
                }
            }

            if (metric is null)
            {
                continue;
            }

            var violation = threshold.Check(metric);
            if (violation is not null)
            {
                violations.Add(violation);
            }
        }

        if (violations.Count > 0)
        {
            throw new QualityGateFailedException(violations);
        }
    }

    private abstract class Threshold
    {
        public string MetricName { get; }

        protected Threshold(string metricName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
            MetricName = metricName;
        }

        public abstract string? Check(EvaluationMetric metric);
    }

    private sealed class NumericMaxThreshold(string metricName, double max) : Threshold(metricName)
    {
        public override string? Check(EvaluationMetric metric)
        {
            if (metric is NumericMetric nm && nm.Value.HasValue && nm.Value.Value > max)
            {
                return $"{MetricName}: {nm.Value.Value:G} exceeded max {max:G}";
            }
            return null;
        }
    }

    private sealed class NumericMinThreshold(string metricName, double min) : Threshold(metricName)
    {
        public override string? Check(EvaluationMetric metric)
        {
            if (metric is NumericMetric nm && nm.Value.HasValue && nm.Value.Value < min)
            {
                return $"{MetricName}: {nm.Value.Value:G} below min {min:G}";
            }
            return null;
        }
    }

    private sealed class BooleanThreshold(string metricName, bool expected) : Threshold(metricName)
    {
        public override string? Check(EvaluationMetric metric)
        {
            if (metric is BooleanMetric bm && bm.Value.HasValue && bm.Value.Value != expected)
            {
                return $"{MetricName}: expected {expected}, got {bm.Value.Value}";
            }
            return null;
        }
    }
}
