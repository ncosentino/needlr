namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Defines one logical experiment case and the number of statistically independent trials to run.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
public sealed record ExperimentCase<TCase>
{
    /// <summary>Gets the stable case identifier, unique within the materialized source.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the caller-owned case value.</summary>
    public required TCase Value { get; init; }

    /// <summary>Gets the number of independent trials to run. Defaults to one.</summary>
    public int TrialCount { get; init; } = 1;

    /// <summary>Gets optional case tags copied into the canonical result.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
