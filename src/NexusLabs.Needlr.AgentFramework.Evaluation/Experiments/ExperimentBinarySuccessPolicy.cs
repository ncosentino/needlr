namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Applies a one-sided Wilson score decision to a required boolean item metric.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed class ExperimentBinarySuccessPolicy<TCase, TOutput> :
    IExperimentRunPolicy<TCase, TOutput>
{
    /// <summary>
    /// Initializes a binary-success statistical policy.
    /// </summary>
    /// <param name="name">The stable policy name.</param>
    /// <param name="metricName">The required boolean item metric name.</param>
    /// <param name="requiredSuccessRate">The required success proportion from zero through one.</param>
    /// <param name="minimumSampleCount">The minimum effective denominator count.</param>
    /// <param name="confidenceLevel">The one-sided confidence level, greater than 0.5 and less than one.</param>
    /// <param name="isRequired">Whether this policy contributes to the run decision.</param>
    /// <param name="unknownSampleTreatment">The treatment for unknown item evidence.</param>
    public ExperimentBinarySuccessPolicy(
        string name,
        string metricName,
        double requiredSuccessRate,
        int minimumSampleCount,
        double confidenceLevel,
        bool isRequired = true,
        ExperimentUnknownSampleTreatment unknownSampleTreatment =
            ExperimentUnknownSampleTreatment.Inconclusive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
        if (!double.IsFinite(requiredSuccessRate)
            || requiredSuccessRate < 0
            || requiredSuccessRate > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredSuccessRate),
                requiredSuccessRate,
                "The required success rate must be finite and between zero and one.");
        }

        if (minimumSampleCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSampleCount),
                minimumSampleCount,
                "The minimum sample count must be positive.");
        }

        if (!double.IsFinite(confidenceLevel)
            || confidenceLevel <= 0.5
            || confidenceLevel >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceLevel),
                confidenceLevel,
                "The one-sided confidence level must be greater than 0.5 and less than one.");
        }

        if (!Enum.IsDefined(unknownSampleTreatment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unknownSampleTreatment),
                unknownSampleTreatment,
                "The unknown sample treatment is not defined.");
        }

        Name = name;
        MetricName = metricName;
        RequiredSuccessRate = requiredSuccessRate;
        MinimumSampleCount = minimumSampleCount;
        ConfidenceLevel = confidenceLevel;
        IsRequired = isRequired;
        UnknownSampleTreatment = unknownSampleTreatment;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Gets the required boolean item metric name.</summary>
    public string MetricName { get; }

    /// <summary>Gets the required success proportion.</summary>
    public double RequiredSuccessRate { get; }

    /// <summary>Gets the minimum effective denominator count.</summary>
    public int MinimumSampleCount { get; }

    /// <summary>Gets the one-sided confidence level.</summary>
    public double ConfidenceLevel { get; }

    /// <summary>Gets the treatment for unknown item evidence.</summary>
    public ExperimentUnknownSampleTreatment UnknownSampleTreatment { get; }

    /// <inheritdoc />
    public ExperimentPolicyKind Kind => ExperimentPolicyKind.Statistical;

    /// <inheritdoc />
    public bool IsRequired { get; }

    /// <inheritdoc />
    public ValueTask<ExperimentPolicyVerdict> EvaluateAsync(
        ExperimentPolicyContext<TCase, TOutput> context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var statusCounts = new int[5];
        var attemptCount = 0;
        var successCount = 0;
        var failureCount = 0;
        var executionFailureCount = 0;
        var exclusionCount = 0;
        foreach (var item in context.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attemptCount += item.Attempts.Count;
            statusCounts[(int)item.Status]++;
            switch (item.Status)
            {
                case ExperimentItemStatus.Succeeded:
                    var metric = item.Metrics.FirstOrDefault(candidate =>
                        string.Equals(candidate.Name, MetricName, StringComparison.Ordinal));
                    if (!TryGetValidBoolean(metric, out var passed))
                    {
                        exclusionCount++;
                    }
                    else if (passed)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                    }

                    break;

                case ExperimentItemStatus.ExecutionFailed:
                    executionFailureCount++;
                    failureCount++;
                    break;

                case ExperimentItemStatus.TimedOut:
                case ExperimentItemStatus.Canceled:
                    failureCount++;
                    break;

                case ExperimentItemStatus.EvaluationFailed:
                    exclusionCount++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(context),
                        item.Status,
                        "The experiment item status is not defined.");
            }
        }

        if (UnknownSampleTreatment == ExperimentUnknownSampleTreatment.CountAsFailure)
        {
            failureCount += exclusionCount;
        }

        var sampleCount = successCount + failureCount;
        double? estimate = null;
        double? lowerBound = null;
        double? upperBound = null;
        if (sampleCount > 0)
        {
            estimate = (double)successCount / sampleCount;
            var z = InverseStandardNormal(ConfidenceLevel);
            (lowerBound, upperBound) = CalculateWilsonBounds(
                successCount,
                sampleCount,
                z);
        }

        var decision =
            UnknownSampleTreatment == ExperimentUnknownSampleTreatment.Inconclusive
            && exclusionCount > 0
                ? EvaluationDecision.Inconclusive
                : sampleCount < MinimumSampleCount
                    ? EvaluationDecision.Inconclusive
                    : lowerBound >= RequiredSuccessRate
                        ? EvaluationDecision.Passed
                        : upperBound < RequiredSuccessRate
                            ? EvaluationDecision.Failed
                            : EvaluationDecision.Inconclusive;
        var statusCountResults = new[]
        {
            new ExperimentItemStatusCount(
                ExperimentItemStatus.Succeeded,
                statusCounts[(int)ExperimentItemStatus.Succeeded]),
            new ExperimentItemStatusCount(
                ExperimentItemStatus.ExecutionFailed,
                statusCounts[(int)ExperimentItemStatus.ExecutionFailed]),
            new ExperimentItemStatusCount(
                ExperimentItemStatus.TimedOut,
                statusCounts[(int)ExperimentItemStatus.TimedOut]),
            new ExperimentItemStatusCount(
                ExperimentItemStatus.Canceled,
                statusCounts[(int)ExperimentItemStatus.Canceled]),
            new ExperimentItemStatusCount(
                ExperimentItemStatus.EvaluationFailed,
                statusCounts[(int)ExperimentItemStatus.EvaluationFailed]),
        };
        return ValueTask.FromResult(new ExperimentPolicyVerdict
        {
            Decision = decision,
            StatisticalEvidence = new ExperimentBinaryStatisticalEvidence
            {
                MetricName = MetricName,
                TotalTrialCount = context.Items.Count,
                AttemptCount = attemptCount,
                SampleCount = sampleCount,
                SuccessCount = successCount,
                FailureCount = failureCount,
                ExecutionFailureCount = executionFailureCount,
                ExclusionCount = exclusionCount,
                StatusCounts = Array.AsReadOnly(statusCountResults),
                Estimate = estimate,
                OneSidedLowerBound = lowerBound,
                OneSidedUpperBound = upperBound,
                ConfidenceLevel = ConfidenceLevel,
                RequiredSuccessRate = RequiredSuccessRate,
                MinimumSampleCount = MinimumSampleCount,
                IntervalMethod = ExperimentConfidenceIntervalMethod.WilsonScore,
                UnknownSampleTreatment = UnknownSampleTreatment,
            },
        });
    }

    private static bool TryGetValidBoolean(
        ExperimentMetricSnapshot? metric,
        out bool value)
    {
        if (metric is null
            || metric.Kind != ExperimentMetricKind.Boolean
            || metric.BooleanValue is not { } booleanValue
            || metric.Interpretation?.Failed == true
            || metric.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == ExperimentMetricDiagnosticSeverity.Error))
        {
            value = false;
            return false;
        }

        value = booleanValue;
        return true;
    }

    private static (double Lower, double Upper) CalculateWilsonBounds(
        int successCount,
        int sampleCount,
        double z)
    {
        var estimate = (double)successCount / sampleCount;
        var zSquared = z * z;
        var denominator = 1 + zSquared / sampleCount;
        var center = estimate + zSquared / (2 * sampleCount);
        var margin = z * Math.Sqrt(
            estimate * (1 - estimate) / sampleCount
            + zSquared / (4d * sampleCount * sampleCount));
        var lower = successCount == 0
            ? 0
            : Math.Max(0, (center - margin) / denominator);
        var upper = successCount == sampleCount
            ? 1
            : Math.Min(1, (center + margin) / denominator);
        return (lower, upper);
    }

    private static double InverseStandardNormal(double probability)
    {
        const double a1 = -3.969683028665376e+01;
        const double a2 = 2.209460984245205e+02;
        const double a3 = -2.759285104469687e+02;
        const double a4 = 1.383577518672690e+02;
        const double a5 = -3.066479806614716e+01;
        const double a6 = 2.506628277459239e+00;
        const double b1 = -5.447609879822406e+01;
        const double b2 = 1.615858368580409e+02;
        const double b3 = -1.556989798598866e+02;
        const double b4 = 6.680131188771972e+01;
        const double b5 = -1.328068155288572e+01;
        const double c1 = -7.784894002430293e-03;
        const double c2 = -3.223964580411365e-01;
        const double c3 = -2.400758277161838e+00;
        const double c4 = -2.549732539343734e+00;
        const double c5 = 4.374664141464968e+00;
        const double c6 = 2.938163982698783e+00;
        const double d1 = 7.784695709041462e-03;
        const double d2 = 3.224671290700398e-01;
        const double d3 = 2.445134137142996e+00;
        const double d4 = 3.754408661907416e+00;
        const double lowerRegion = 0.02425;
        const double upperRegion = 1 - lowerRegion;
        if (probability < lowerRegion)
        {
            var q = Math.Sqrt(-2 * Math.Log(probability));
            return (((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6)
                / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
        }

        if (probability <= upperRegion)
        {
            var q = probability - 0.5;
            var r = q * q;
            return (((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6)
                * q
                / (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1);
        }

        var upperQ = Math.Sqrt(-2 * Math.Log(1 - probability));
        return -(((((c1 * upperQ + c2) * upperQ + c3) * upperQ + c4) * upperQ + c5)
                * upperQ + c6)
            / ((((d1 * upperQ + d2) * upperQ + d3) * upperQ + d4) * upperQ + 1);
    }
}
