namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one item scope's publication outcome independently from item quality.
/// </summary>
public sealed class ExperimentItemPublicationResult
{
    /// <summary>Gets the unique item-scope provider name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether publication failure is required for aggregate publication
    /// health.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>Gets the publication status.</summary>
    public required ExperimentItemPublicationStatus Status { get; init; }

    /// <summary>Gets namespaced provider identifiers produced by this scope.</summary>
    public IReadOnlyList<ExperimentItemCorrelation> Correlations { get; init; } = [];

    /// <summary>Gets the structured publication failure, when present.</summary>
    public ExperimentFailure? Failure { get; init; }
}
