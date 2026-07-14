namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides an immutable normalized metric interpretation.
/// </summary>
public sealed class ExperimentMetricInterpretationSnapshot
{
    /// <summary>Gets the normalized rating.</summary>
    public required ExperimentMetricRating Rating { get; init; }

    /// <summary>Gets a value indicating whether MEAI interpreted the metric as failed.</summary>
    public required bool Failed { get; init; }

    /// <summary>Gets the optional interpretation reason.</summary>
    public string? Reason { get; init; }
}
