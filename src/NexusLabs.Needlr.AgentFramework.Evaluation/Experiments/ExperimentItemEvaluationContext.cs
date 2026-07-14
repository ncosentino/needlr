namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a successful task output and its stable identity to an item evaluator.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed class ExperimentItemEvaluationContext<TCase, TOutput>
{
    internal ExperimentItemEvaluationContext(
        string runId,
        int sequence,
        ExperimentCase<TCase> @case,
        int trialIndex,
        TOutput output,
        IReadOnlyList<ExperimentAttemptResult> attempts)
    {
        RunId = runId;
        Sequence = sequence;
        Case = @case;
        TrialIndex = trialIndex;
        Output = output;
        Attempts = attempts;
    }

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the zero-based stable item sequence.</summary>
    public int Sequence { get; }

    /// <summary>Gets the materialized case.</summary>
    public ExperimentCase<TCase> Case { get; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public int TrialIndex { get; }

    /// <summary>Gets the terminal successful task output.</summary>
    public TOutput Output { get; }

    /// <summary>Gets the complete operational attempt history.</summary>
    public IReadOnlyList<ExperimentAttemptResult> Attempts { get; }
}
