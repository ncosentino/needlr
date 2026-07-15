namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the provider-neutral source from which experiment cases were materialized.
/// </summary>
public sealed record ExperimentSourceReference
{
    /// <summary>Gets the human-readable source name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets an optional stable source identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Gets an optional source version.</summary>
    public string? Version { get; init; }
}
