namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Describes the structured outcome of one metric threshold.
/// </summary>
public sealed class EvaluationThresholdOutcome
{
    /// <summary>Gets the metric name.</summary>
    public required string MetricName { get; init; }

    /// <summary>Gets the comparison kind.</summary>
    public required EvaluationThresholdKind Kind { get; init; }

    /// <summary>Gets the threshold outcome.</summary>
    public required EvaluationThresholdStatus Status { get; init; }

    /// <summary>Gets a value indicating whether missing evidence is required.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Gets the configured numeric bound, when applicable.</summary>
    public double? NumericThreshold { get; init; }

    /// <summary>Gets the configured boolean value, when applicable.</summary>
    public bool? BooleanExpected { get; init; }

    /// <summary>Gets the observed numeric value, when available.</summary>
    public double? NumericValue { get; init; }

    /// <summary>Gets the observed boolean value, when available.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets a stable human-readable description of the outcome.</summary>
    public required string Message { get; init; }
}
