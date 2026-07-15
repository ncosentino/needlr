namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides stable identity and case data to one experiment task attempt.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
public sealed record ExperimentTaskContext<TCase>
{
    internal ExperimentTaskContext(
        string runId,
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex,
        int attemptNumber,
        ExperimentItemFeatureCollection features)
    {
        RunId = runId;
        Sequence = sequence;
        Case = @case;
        TrialIndex = trialIndex;
        AttemptNumber = attemptNumber;
        Features = features;
    }

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the zero-based stable item sequence.</summary>
    public int Sequence { get; }

    /// <summary>Gets the materialized case.</summary>
    public ExperimentCase<TCase> Case { get; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public int TrialIndex { get; }

    /// <summary>Gets the one-based operational attempt number.</summary>
    public int AttemptNumber { get; }

    /// <summary>Gets adapter-owned features for this statistical trial.</summary>
    public ExperimentItemFeatureCollection Features { get; }
}
