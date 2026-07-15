namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one final result sink's publication outcome independently from run quality.
/// </summary>
public sealed record ExperimentSinkResult
{
    /// <summary>Gets the unique result-sink name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether failure contributes to aggregate required-publication
    /// failure.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>Gets the publication operation status.</summary>
    public required ExperimentPublicationOperationStatus Status { get; init; }

    /// <summary>Gets the structured publication failure, when present.</summary>
    public ExperimentFailure? Failure { get; init; }
}
