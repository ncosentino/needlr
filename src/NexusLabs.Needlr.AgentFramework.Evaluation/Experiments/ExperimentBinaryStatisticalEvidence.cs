namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes binary-success proportion evidence and its uncertainty.
/// </summary>
public sealed record ExperimentBinaryStatisticalEvidence
{
    /// <summary>Gets the required boolean metric name.</summary>
    public required string MetricName { get; init; }

    /// <summary>Gets the number of statistical trials in the run.</summary>
    public required int TotalTrialCount { get; init; }

    /// <summary>Gets the total number of operational attempts across all trials.</summary>
    public required int AttemptCount { get; init; }

    /// <summary>Gets the effective denominator sample count.</summary>
    public required int SampleCount { get; init; }

    /// <summary>Gets the denominator success count.</summary>
    public required int SuccessCount { get; init; }

    /// <summary>Gets the denominator failure count.</summary>
    public required int FailureCount { get; init; }

    /// <summary>Gets the number of execution-failed trials.</summary>
    public required int ExecutionFailureCount { get; init; }

    /// <summary>Gets the number of trials with unknown or excluded evidence.</summary>
    public required int ExclusionCount { get; init; }

    /// <summary>Gets item counts in stable status order.</summary>
    public required IReadOnlyList<ExperimentItemStatusCount> StatusCounts { get; init; }

    /// <summary>Gets the observed success proportion, when a denominator exists.</summary>
    public double? Estimate { get; init; }

    /// <summary>
    /// Gets the one-sided lower bound at <see cref="ConfidenceLevel"/>, when a denominator exists.
    /// </summary>
    public double? OneSidedLowerBound { get; init; }

    /// <summary>
    /// Gets the one-sided upper bound at <see cref="ConfidenceLevel"/>, when a denominator exists.
    /// </summary>
    public double? OneSidedUpperBound { get; init; }

    /// <summary>Gets the one-sided confidence level.</summary>
    public required double ConfidenceLevel { get; init; }

    /// <summary>Gets the required success proportion.</summary>
    public required double RequiredSuccessRate { get; init; }

    /// <summary>Gets the minimum effective sample count.</summary>
    public required int MinimumSampleCount { get; init; }

    /// <summary>Gets the confidence interval method.</summary>
    public required ExperimentConfidenceIntervalMethod IntervalMethod { get; init; }

    /// <summary>Gets the configured treatment for unknown samples.</summary>
    public required ExperimentUnknownSampleTreatment UnknownSampleTreatment { get; init; }
}
