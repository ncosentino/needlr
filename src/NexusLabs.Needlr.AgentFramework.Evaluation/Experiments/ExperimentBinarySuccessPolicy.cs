namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Applies a one-sided Wilson score decision to a required boolean item metric.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
[DoNotAutoRegister]
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="metricName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="metricName"/> is empty or consists only of
    /// white-space characters.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requiredSuccessRate"/>, <paramref name="minimumSampleCount"/>, or
    /// <paramref name="confidenceLevel"/> is outside its supported range.
    /// </exception>
    public ExperimentBinarySuccessPolicy(
        string name,
        string metricName,
        double requiredSuccessRate,
        int minimumSampleCount,
        double confidenceLevel)
        : this(
            name,
            metricName,
            requiredSuccessRate,
            minimumSampleCount,
            confidenceLevel,
            isRequired: true,
            unknownSampleTreatment: ExperimentUnknownSampleTreatment.Inconclusive)
    {
    }

    /// <summary>
    /// Initializes a binary-success statistical policy with an explicit required flag and
    /// unknown-sample treatment.
    /// </summary>
    /// <param name="name">The stable policy name.</param>
    /// <param name="metricName">The required boolean item metric name.</param>
    /// <param name="requiredSuccessRate">The required success proportion from zero through one.</param>
    /// <param name="minimumSampleCount">The minimum effective denominator count.</param>
    /// <param name="confidenceLevel">The one-sided confidence level, greater than 0.5 and less than one.</param>
    /// <param name="isRequired">Whether this policy contributes to the run decision.</param>
    /// <param name="unknownSampleTreatment">The treatment for unknown item evidence.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="metricName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="metricName"/> is empty or consists only of
    /// white-space characters.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requiredSuccessRate"/>, <paramref name="minimumSampleCount"/>,
    /// <paramref name="confidenceLevel"/>, or <paramref name="unknownSampleTreatment"/> is outside
    /// its supported range.
    /// </exception>
    public ExperimentBinarySuccessPolicy(
        string name,
        string metricName,
        double requiredSuccessRate,
        int minimumSampleCount,
        double confidenceLevel,
        bool isRequired,
        ExperimentUnknownSampleTreatment unknownSampleTreatment)
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
        var statusCounts = new int[6];
        var attemptCount = 0;
        var successCount = 0;
        var failureCount = 0;
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
                    failureCount++;
                    break;

                case ExperimentItemStatus.TimedOut:
                case ExperimentItemStatus.Canceled:
                    failureCount++;
                    break;

                case ExperimentItemStatus.EvaluationFailed:
                case ExperimentItemStatus.PrerequisiteFailed:
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
            new ExperimentItemStatusCount(
                ExperimentItemStatus.PrerequisiteFailed,
                statusCounts[(int)ExperimentItemStatus.PrerequisiteFailed]),
        };
        var evidence = ExperimentBinaryStatisticalEvidence.Create(
            MetricName,
            attemptCount,
            successCount,
            failureCount,
            exclusionCount,
            Array.AsReadOnly(statusCountResults),
            ConfidenceLevel,
            RequiredSuccessRate,
            MinimumSampleCount,
            ExperimentConfidenceIntervalMethod.WilsonScore,
            UnknownSampleTreatment);
        return ValueTask.FromResult(
            ExperimentPolicyVerdict.FromStatisticalEvidence(evidence));
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

}
