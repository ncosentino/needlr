namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides one namespaced provider identifier associated with an experiment item.
/// </summary>
public sealed record ExperimentItemCorrelation
{
    /// <summary>Gets the provider-owned correlation namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary>Gets the identifier name within <see cref="Namespace"/>.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the provider identifier value.</summary>
    public required string Value { get; init; }
}
