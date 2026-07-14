namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Contains a finite ordered case collection and its provider-neutral source identity.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
public sealed class ExperimentCaseSourceResult<TCase>
{
    /// <summary>Gets the source identity.</summary>
    public required ExperimentSourceReference Source { get; init; }

    /// <summary>Gets the finite ordered case collection.</summary>
    public required IReadOnlyList<ExperimentCase<TCase>> Cases { get; init; }
}
