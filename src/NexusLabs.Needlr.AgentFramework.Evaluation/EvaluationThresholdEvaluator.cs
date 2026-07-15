using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Configures and evaluates reusable structured metric thresholds.
/// </summary>
/// <remarks>
/// Configure an instance before sharing it across concurrent evaluations. Evaluation copies the
/// configured threshold list before reading metrics.
/// </remarks>
[DoNotAutoRegister]
public sealed class EvaluationThresholdEvaluator
{
    private readonly List<Threshold> _thresholds = [];

    /// <summary>
    /// Adds a required numeric maximum threshold.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="max">The inclusive maximum.</param>
    /// <returns>This evaluator for fluent configuration.</returns>
    public EvaluationThresholdEvaluator RequireNumericMax(string metricName, double max)
    {
        _thresholds.Add(new NumericMaximumThreshold(metricName, max, isRequired: true));
        return this;
    }

    /// <summary>
    /// Adds a numeric maximum threshold that permits the metric to be absent.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="max">The inclusive maximum.</param>
    /// <returns>This evaluator for fluent configuration.</returns>
    public EvaluationThresholdEvaluator OptionalNumericMax(string metricName, double max)
    {
        _thresholds.Add(new NumericMaximumThreshold(metricName, max, isRequired: false));
        return this;
    }

    /// <summary>
    /// Adds a required numeric minimum threshold.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="min">The inclusive minimum.</param>
    /// <returns>This evaluator for fluent configuration.</returns>
    public EvaluationThresholdEvaluator RequireNumericMin(string metricName, double min)
    {
        _thresholds.Add(new NumericMinimumThreshold(metricName, min, isRequired: true));
        return this;
    }

    /// <summary>
    /// Adds a numeric minimum threshold that permits the metric to be absent.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="min">The inclusive minimum.</param>
    /// <returns>This evaluator for fluent configuration.</returns>
    public EvaluationThresholdEvaluator OptionalNumericMin(string metricName, double min)
    {
        _thresholds.Add(new NumericMinimumThreshold(metricName, min, isRequired: false));
        return this;
    }

    /// <summary>
    /// Adds a required boolean threshold.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="expected">The required value.</param>
    /// <returns>This evaluator for fluent configuration.</returns>
    public EvaluationThresholdEvaluator RequireBoolean(string metricName, bool expected)
    {
        _thresholds.Add(new BooleanThreshold(metricName, expected, isRequired: true));
        return this;
    }

    /// <summary>
    /// Adds a boolean threshold that permits the metric to be absent.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="expected">The required value when the metric is present.</param>
    /// <returns>This evaluator for fluent configuration.</returns>
    public EvaluationThresholdEvaluator OptionalBoolean(string metricName, bool expected)
    {
        _thresholds.Add(new BooleanThreshold(metricName, expected, isRequired: false));
        return this;
    }

    /// <summary>
    /// Evaluates configured thresholds against one or more MEAI evaluation results.
    /// </summary>
    /// <param name="results">The results searched in order; the first metric-name match wins.</param>
    /// <returns>The structured threshold result.</returns>
    public EvaluationThresholdResult Evaluate(params EvaluationResult[] results) =>
        Evaluate(EvaluationMissingMetricBehavior.Inconclusive, results);

    /// <summary>
    /// Evaluates configured thresholds against one or more MEAI evaluation results.
    /// </summary>
    /// <param name="missingMetricBehavior">
    /// The treatment for required missing or invalid metrics.
    /// </param>
    /// <param name="results">The results searched in order; the first metric-name match wins.</param>
    /// <returns>The structured threshold result.</returns>
    public EvaluationThresholdResult Evaluate(
        EvaluationMissingMetricBehavior missingMetricBehavior,
        params EvaluationResult[] results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (!Enum.IsDefined(missingMetricBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingMetricBehavior),
                missingMetricBehavior,
                "The missing metric behavior is not defined.");
        }

        foreach (var result in results)
        {
            ArgumentNullException.ThrowIfNull(result);
        }

        return EvaluateCore(
            metricName => FindMetric(results, metricName),
            missingMetricBehavior);
    }

    internal EvaluationThresholdResult Evaluate(
        IReadOnlyList<ExperimentMetricSnapshot> metrics,
        EvaluationMissingMetricBehavior missingMetricBehavior)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (!Enum.IsDefined(missingMetricBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingMetricBehavior),
                missingMetricBehavior,
                "The missing metric behavior is not defined.");
        }

        return EvaluateCore(
            metricName => FindMetric(metrics, metricName),
            missingMetricBehavior);
    }

    private EvaluationThresholdResult EvaluateCore(
        Func<string, MetricValue?> findMetric,
        EvaluationMissingMetricBehavior missingMetricBehavior)
    {
        var thresholds = _thresholds.ToArray();
        var outcomes = new EvaluationThresholdOutcome[thresholds.Length];
        var hasFailure = false;
        var hasUnknownRequiredEvidence = false;
        for (var index = 0; index < thresholds.Length; index++)
        {
            var outcome = thresholds[index].Evaluate(findMetric(thresholds[index].MetricName));
            outcomes[index] = outcome;
            hasFailure |= outcome.Status == EvaluationThresholdStatus.Failed;
            hasUnknownRequiredEvidence |=
                outcome.Status == EvaluationThresholdStatus.Invalid
                || (outcome.IsRequired && outcome.Status == EvaluationThresholdStatus.Missing);
        }

        var decision = hasFailure
            ? EvaluationDecision.Failed
            : hasUnknownRequiredEvidence
                ? missingMetricBehavior == EvaluationMissingMetricBehavior.Fail
                    ? EvaluationDecision.Failed
                    : EvaluationDecision.Inconclusive
                : EvaluationDecision.Passed;
        return new EvaluationThresholdResult
        {
            Decision = decision,
            Outcomes = Array.AsReadOnly(outcomes),
        };
    }

    private static MetricValue? FindMetric(
        IReadOnlyList<EvaluationResult> results,
        string metricName)
    {
        foreach (var result in results)
        {
            if (!result.Metrics.TryGetValue(metricName, out var metric))
            {
                continue;
            }

            return metric switch
            {
                NumericMetric numeric => new MetricValue(
                    MetricValueKind.Numeric,
                    numeric.Value is { } value && double.IsFinite(value) ? value : null,
                    null),
                BooleanMetric boolean => new MetricValue(
                    MetricValueKind.Boolean,
                    null,
                    boolean.Value),
                _ => new MetricValue(MetricValueKind.Other, null, null),
            };
        }

        return null;
    }

    private static MetricValue? FindMetric(
        IReadOnlyList<ExperimentMetricSnapshot> metrics,
        string metricName)
    {
        foreach (var metric in metrics)
        {
            if (!string.Equals(metric.Name, metricName, StringComparison.Ordinal))
            {
                continue;
            }

            return metric.Kind switch
            {
                ExperimentMetricKind.Numeric => new MetricValue(
                    MetricValueKind.Numeric,
                    metric.NumericValue,
                    null),
                ExperimentMetricKind.Boolean => new MetricValue(
                    MetricValueKind.Boolean,
                    null,
                    metric.BooleanValue),
                _ => new MetricValue(MetricValueKind.Other, null, null),
            };
        }

        return null;
    }

    private abstract class Threshold
    {
        protected Threshold(string metricName, bool isRequired)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
            MetricName = metricName;
            IsRequired = isRequired;
        }

        public string MetricName { get; }

        protected bool IsRequired { get; }

        public abstract EvaluationThresholdOutcome Evaluate(MetricValue? metric);

        protected EvaluationThresholdOutcome Missing(EvaluationThresholdKind kind) =>
            new()
            {
                MetricName = MetricName,
                Kind = kind,
                Status = EvaluationThresholdStatus.Missing,
                IsRequired = IsRequired,
                Message = IsRequired
                    ? $"{MetricName}: required metric was not found"
                    : $"{MetricName}: optional metric was not found",
            };
    }

    private sealed class NumericMaximumThreshold : Threshold
    {
        private readonly double _maximum;

        public NumericMaximumThreshold(string metricName, double maximum, bool isRequired)
            : base(metricName, isRequired)
        {
            if (!double.IsFinite(maximum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum),
                    maximum,
                    "A numeric maximum threshold must be finite.");
            }

            _maximum = maximum;
        }

        public override EvaluationThresholdOutcome Evaluate(MetricValue? metric)
        {
            if (metric is null)
            {
                return Missing(EvaluationThresholdKind.NumericMaximum);
            }

            if (metric.Value.Kind != MetricValueKind.Numeric
                || metric.Value.NumericValue is not { } value)
            {
                return new EvaluationThresholdOutcome
                {
                    MetricName = MetricName,
                    Kind = EvaluationThresholdKind.NumericMaximum,
                    Status = EvaluationThresholdStatus.Invalid,
                    IsRequired = IsRequired,
                    NumericThreshold = _maximum,
                    Message = $"{MetricName}: expected a finite numeric metric",
                };
            }

            return new EvaluationThresholdOutcome
            {
                MetricName = MetricName,
                Kind = EvaluationThresholdKind.NumericMaximum,
                Status = value <= _maximum
                    ? EvaluationThresholdStatus.Passed
                    : EvaluationThresholdStatus.Failed,
                IsRequired = IsRequired,
                NumericThreshold = _maximum,
                NumericValue = value,
                Message = value <= _maximum
                    ? $"{MetricName}: {value:G} met max {_maximum:G}"
                    : $"{MetricName}: {value:G} exceeded max {_maximum:G}",
            };
        }
    }

    private sealed class NumericMinimumThreshold : Threshold
    {
        private readonly double _minimum;

        public NumericMinimumThreshold(string metricName, double minimum, bool isRequired)
            : base(metricName, isRequired)
        {
            if (!double.IsFinite(minimum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimum),
                    minimum,
                    "A numeric minimum threshold must be finite.");
            }

            _minimum = minimum;
        }

        public override EvaluationThresholdOutcome Evaluate(MetricValue? metric)
        {
            if (metric is null)
            {
                return Missing(EvaluationThresholdKind.NumericMinimum);
            }

            if (metric.Value.Kind != MetricValueKind.Numeric
                || metric.Value.NumericValue is not { } value)
            {
                return new EvaluationThresholdOutcome
                {
                    MetricName = MetricName,
                    Kind = EvaluationThresholdKind.NumericMinimum,
                    Status = EvaluationThresholdStatus.Invalid,
                    IsRequired = IsRequired,
                    NumericThreshold = _minimum,
                    Message = $"{MetricName}: expected a finite numeric metric",
                };
            }

            return new EvaluationThresholdOutcome
            {
                MetricName = MetricName,
                Kind = EvaluationThresholdKind.NumericMinimum,
                Status = value >= _minimum
                    ? EvaluationThresholdStatus.Passed
                    : EvaluationThresholdStatus.Failed,
                IsRequired = IsRequired,
                NumericThreshold = _minimum,
                NumericValue = value,
                Message = value >= _minimum
                    ? $"{MetricName}: {value:G} met min {_minimum:G}"
                    : $"{MetricName}: {value:G} below min {_minimum:G}",
            };
        }
    }

    private sealed class BooleanThreshold : Threshold
    {
        private readonly bool _expected;

        public BooleanThreshold(string metricName, bool expected, bool isRequired)
            : base(metricName, isRequired)
        {
            _expected = expected;
        }

        public override EvaluationThresholdOutcome Evaluate(MetricValue? metric)
        {
            if (metric is null)
            {
                return Missing(EvaluationThresholdKind.Boolean);
            }

            if (metric.Value.Kind != MetricValueKind.Boolean
                || metric.Value.BooleanValue is not { } value)
            {
                return new EvaluationThresholdOutcome
                {
                    MetricName = MetricName,
                    Kind = EvaluationThresholdKind.Boolean,
                    Status = EvaluationThresholdStatus.Invalid,
                    IsRequired = IsRequired,
                    BooleanExpected = _expected,
                    Message = $"{MetricName}: expected a boolean metric",
                };
            }

            return new EvaluationThresholdOutcome
            {
                MetricName = MetricName,
                Kind = EvaluationThresholdKind.Boolean,
                Status = value == _expected
                    ? EvaluationThresholdStatus.Passed
                    : EvaluationThresholdStatus.Failed,
                IsRequired = IsRequired,
                BooleanExpected = _expected,
                BooleanValue = value,
                Message = value == _expected
                    ? $"{MetricName}: matched expected {_expected}"
                    : $"{MetricName}: expected {_expected}, got {value}",
            };
        }
    }

    private enum MetricValueKind
    {
        Numeric,
        Boolean,
        Other,
    }

    private readonly record struct MetricValue(
        MetricValueKind Kind,
        double? NumericValue,
        bool? BooleanValue);
}
