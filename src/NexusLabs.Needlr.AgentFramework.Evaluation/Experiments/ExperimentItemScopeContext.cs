namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides stable identity and case data when entering one per-trial item scope.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
public sealed record ExperimentItemScopeContext<TCase>
{
    internal ExperimentItemScopeContext(
        string runId,
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex)
    {
        RunId = runId;
        Sequence = sequence;
        Case = @case;
        TrialIndex = trialIndex;
    }

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the zero-based stable item sequence.</summary>
    public int Sequence { get; }

    /// <summary>Gets the materialized case.</summary>
    public ExperimentCase<TCase> Case { get; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public int TrialIndex { get; }
}
