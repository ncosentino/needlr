namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes binary-success proportion evidence and its uncertainty.
/// </summary>
public sealed record ExperimentBinaryStatisticalEvidence
{
    private static readonly ExperimentItemStatus[] StatusOrder =
    [
        ExperimentItemStatus.Succeeded,
        ExperimentItemStatus.ExecutionFailed,
        ExperimentItemStatus.TimedOut,
        ExperimentItemStatus.Canceled,
        ExperimentItemStatus.EvaluationFailed,
        ExperimentItemStatus.PrerequisiteFailed,
    ];

    private ExperimentBinaryStatisticalEvidence(
        string metricName,
        int totalTrialCount,
        int attemptCount,
        int sampleCount,
        int successCount,
        int failureCount,
        int executionFailureCount,
        int exclusionCount,
        IReadOnlyList<ExperimentItemStatusCount> statusCounts,
        double? estimate,
        double? oneSidedLowerBound,
        double? oneSidedUpperBound,
        double confidenceLevel,
        double requiredSuccessRate,
        int minimumSampleCount,
        ExperimentConfidenceIntervalMethod intervalMethod,
        ExperimentUnknownSampleTreatment unknownSampleTreatment)
    {
        MetricName = metricName;
        TotalTrialCount = totalTrialCount;
        AttemptCount = attemptCount;
        SampleCount = sampleCount;
        SuccessCount = successCount;
        FailureCount = failureCount;
        ExecutionFailureCount = executionFailureCount;
        ExclusionCount = exclusionCount;
        StatusCounts = statusCounts;
        Estimate = estimate;
        OneSidedLowerBound = oneSidedLowerBound;
        OneSidedUpperBound = oneSidedUpperBound;
        ConfidenceLevel = confidenceLevel;
        RequiredSuccessRate = requiredSuccessRate;
        MinimumSampleCount = minimumSampleCount;
        IntervalMethod = intervalMethod;
        UnknownSampleTreatment = unknownSampleTreatment;
    }

    /// <summary>Gets the required boolean metric name.</summary>
    public string MetricName { get; }

    /// <summary>Gets the number of statistical trials in the run.</summary>
    public int TotalTrialCount { get; }

    /// <summary>Gets the total number of operational attempts across all trials.</summary>
    public int AttemptCount { get; }

    /// <summary>Gets the effective denominator sample count.</summary>
    public int SampleCount { get; }

    /// <summary>Gets the denominator success count.</summary>
    public int SuccessCount { get; }

    /// <summary>Gets the denominator failure count.</summary>
    public int FailureCount { get; }

    /// <summary>Gets the number of execution-failed trials.</summary>
    public int ExecutionFailureCount { get; }

    /// <summary>Gets the number of trials with unknown or excluded evidence.</summary>
    public int ExclusionCount { get; }

    /// <summary>Gets item counts in stable status order.</summary>
    public IReadOnlyList<ExperimentItemStatusCount> StatusCounts { get; }

    /// <summary>Gets the observed success proportion, when a denominator exists.</summary>
    public double? Estimate { get; }

    /// <summary>
    /// Gets the one-sided lower bound at <see cref="ConfidenceLevel"/>, when a denominator exists.
    /// </summary>
    public double? OneSidedLowerBound { get; }

    /// <summary>
    /// Gets the one-sided upper bound at <see cref="ConfidenceLevel"/>, when a denominator exists.
    /// </summary>
    public double? OneSidedUpperBound { get; }

    /// <summary>Gets the one-sided confidence level.</summary>
    public double ConfidenceLevel { get; }

    /// <summary>Gets the required success proportion.</summary>
    public double RequiredSuccessRate { get; }

    /// <summary>Gets the minimum effective sample count.</summary>
    public int MinimumSampleCount { get; }

    /// <summary>Gets the confidence interval method.</summary>
    public ExperimentConfidenceIntervalMethod IntervalMethod { get; }

    /// <summary>Gets the configured treatment for unknown samples.</summary>
    public ExperimentUnknownSampleTreatment UnknownSampleTreatment { get; }

    /// <summary>Creates validated binary statistical evidence.</summary>
    /// <param name="metricName">The required boolean metric name.</param>
    /// <param name="attemptCount">The total number of operational attempts.</param>
    /// <param name="successCount">The effective denominator success count.</param>
    /// <param name="failureCount">The effective denominator failure count.</param>
    /// <param name="exclusionCount">The number of trials with excluded evidence.</param>
    /// <param name="statusCounts">Item counts for every terminal status.</param>
    /// <param name="confidenceLevel">The one-sided confidence level.</param>
    /// <param name="requiredSuccessRate">The required success proportion.</param>
    /// <param name="minimumSampleCount">The minimum effective sample count.</param>
    /// <param name="intervalMethod">The confidence interval method.</param>
    /// <param name="unknownSampleTreatment">The treatment for excluded evidence.</param>
    /// <returns>Validated binary statistical evidence with derived totals and Wilson bounds.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="statusCounts"/> or one of its elements is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="metricName"/> is blank, status accounting is incomplete or inconsistent, or
    /// sample and exclusion counts do not match the configured unknown-sample treatment.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A count is negative, a configured rate or confidence level is outside its supported range,
    /// the minimum sample count is not positive, or an enum value is undefined.
    /// </exception>
    public static ExperimentBinaryStatisticalEvidence Create(
        string metricName,
        int attemptCount,
        int successCount,
        int failureCount,
        int exclusionCount,
        IReadOnlyList<ExperimentItemStatusCount> statusCounts,
        double confidenceLevel,
        double requiredSuccessRate,
        int minimumSampleCount,
        ExperimentConfidenceIntervalMethod intervalMethod,
        ExperimentUnknownSampleTreatment unknownSampleTreatment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
        ValidateNonNegative(attemptCount, nameof(attemptCount));
        ValidateNonNegative(successCount, nameof(successCount));
        ValidateNonNegative(failureCount, nameof(failureCount));
        ValidateNonNegative(exclusionCount, nameof(exclusionCount));
        if (!double.IsFinite(confidenceLevel)
            || confidenceLevel <= 0.5
            || confidenceLevel >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceLevel),
                confidenceLevel,
                "The one-sided confidence level must be greater than 0.5 and less than one.");
        }

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

        if (!Enum.IsDefined(intervalMethod))
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMethod),
                intervalMethod,
                "The confidence interval method is not defined.");
        }

        if (!Enum.IsDefined(unknownSampleTreatment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unknownSampleTreatment),
                unknownSampleTreatment,
                "The unknown sample treatment is not defined.");
        }

        var statusSnapshot = SnapshotStatusCounts(statusCounts);
        var totalTrialCount = checked(statusSnapshot.Sum(status => status.Count));
        var succeededItemCount = GetStatusCount(
            statusSnapshot,
            ExperimentItemStatus.Succeeded);
        var executionFailureCount = GetStatusCount(
            statusSnapshot,
            ExperimentItemStatus.ExecutionFailed);
        var timedOutCount = GetStatusCount(
            statusSnapshot,
            ExperimentItemStatus.TimedOut);
        var canceledCount = GetStatusCount(
            statusSnapshot,
            ExperimentItemStatus.Canceled);
        var evaluationFailureCount = GetStatusCount(
            statusSnapshot,
            ExperimentItemStatus.EvaluationFailed);
        var prerequisiteFailureCount = GetStatusCount(
            statusSnapshot,
            ExperimentItemStatus.PrerequisiteFailed);
        if (attemptCount < totalTrialCount - prerequisiteFailureCount)
        {
            throw new ArgumentException(
                "The attempt count cannot be smaller than the number of trials that executed.",
                nameof(attemptCount));
        }

        if (successCount > succeededItemCount)
        {
            throw new ArgumentException(
                "The success count cannot exceed the number of succeeded items.",
                nameof(successCount));
        }

        if (exclusionCount < evaluationFailureCount + prerequisiteFailureCount)
        {
            throw new ArgumentException(
                "The exclusion count must include evaluation and prerequisite failures.",
                nameof(exclusionCount));
        }

        var sampleCount = checked(successCount + failureCount);
        var minimumFailureCount = executionFailureCount + timedOutCount + canceledCount;
        if (unknownSampleTreatment == ExperimentUnknownSampleTreatment.Inconclusive)
        {
            if (checked(sampleCount + exclusionCount) != totalTrialCount)
            {
                throw new ArgumentException(
                    "Samples plus exclusions must equal the total trial count.",
                    nameof(exclusionCount));
            }
        }
        else if (sampleCount != totalTrialCount
            || failureCount < minimumFailureCount + exclusionCount)
        {
            throw new ArgumentException(
                "Pessimistic unknown-sample treatment requires every trial in the denominator and every exclusion counted as a failure.",
                nameof(failureCount));
        }

        if (unknownSampleTreatment == ExperimentUnknownSampleTreatment.Inconclusive
            && failureCount < minimumFailureCount)
        {
            throw new ArgumentException(
                "The failure count must include execution failures, timeouts, and cancellations.",
                nameof(failureCount));
        }

        double? estimate = null;
        double? lowerBound = null;
        double? upperBound = null;
        if (sampleCount > 0)
        {
            (estimate, lowerBound, upperBound) = ExperimentWilsonScoreCalculator.Calculate(
                successCount,
                sampleCount,
                confidenceLevel);
        }

        return new ExperimentBinaryStatisticalEvidence(
            metricName,
            totalTrialCount,
            attemptCount,
            sampleCount,
            successCount,
            failureCount,
            executionFailureCount,
            exclusionCount,
            statusSnapshot,
            estimate,
            lowerBound,
            upperBound,
            confidenceLevel,
            requiredSuccessRate,
            minimumSampleCount,
            intervalMethod,
            unknownSampleTreatment);
    }

    private static IReadOnlyList<ExperimentItemStatusCount> SnapshotStatusCounts(
        IReadOnlyList<ExperimentItemStatusCount> statusCounts)
    {
        ArgumentNullException.ThrowIfNull(statusCounts);
        var byStatus = new Dictionary<ExperimentItemStatus, int>();
        foreach (var statusCount in statusCounts)
        {
            ArgumentNullException.ThrowIfNull(statusCount);
            if (!byStatus.TryAdd(statusCount.Status, statusCount.Count))
            {
                throw new ArgumentException(
                    $"Item status '{statusCount.Status}' appears more than once.",
                    nameof(statusCounts));
            }
        }

        if (byStatus.Count != StatusOrder.Length
            || StatusOrder.Any(status => !byStatus.ContainsKey(status)))
        {
            throw new ArgumentException(
                "Status counts must contain every experiment item status exactly once.",
                nameof(statusCounts));
        }

        return Array.AsReadOnly(StatusOrder
            .Select(status => new ExperimentItemStatusCount(status, byStatus[status]))
            .ToArray());
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Statistical evidence counts must be non-negative.");
        }
    }

    private static int GetStatusCount(
        IReadOnlyList<ExperimentItemStatusCount> statusCounts,
        ExperimentItemStatus status) =>
        statusCounts.Single(count => count.Status == status).Count;
}
