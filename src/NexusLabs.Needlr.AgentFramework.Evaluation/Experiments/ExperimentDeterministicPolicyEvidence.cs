namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes deterministic threshold evidence from one named run evaluation.
/// </summary>
public sealed record ExperimentDeterministicPolicyEvidence
{
    private ExperimentDeterministicPolicyEvidence(
        string runEvaluationName,
        EvaluationThresholdResult? thresholds,
        string? unavailableReason)
    {
        RunEvaluationName = runEvaluationName;
        Thresholds = thresholds;
        UnavailableReason = unavailableReason;
    }

    /// <summary>Gets the run-evaluator name that supplied metrics.</summary>
    public string RunEvaluationName { get; }

    /// <summary>Gets the structured threshold result when metrics were available.</summary>
    public EvaluationThresholdResult? Thresholds { get; }

    /// <summary>Gets why the named run evaluation could not supply metrics.</summary>
    public string? UnavailableReason { get; }

    /// <summary>Creates evidence from an available threshold result.</summary>
    /// <param name="runEvaluationName">The run-evaluator name that supplied metrics.</param>
    /// <param name="thresholds">The structured threshold result.</param>
    /// <returns>Available deterministic policy evidence.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="runEvaluationName"/> is empty or consists only of white-space characters.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="thresholds"/>, its outcome collection, or one of its outcomes is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The threshold decision is not defined.
    /// </exception>
    public static ExperimentDeterministicPolicyEvidence Available(
        string runEvaluationName,
        EvaluationThresholdResult thresholds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runEvaluationName);
        ArgumentNullException.ThrowIfNull(thresholds);
        if (!Enum.IsDefined(thresholds.Decision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(thresholds),
                thresholds.Decision,
                "The threshold decision is not defined.");
        }

        ArgumentNullException.ThrowIfNull(thresholds.Outcomes);
        var outcomes = new EvaluationThresholdOutcome[thresholds.Outcomes.Count];
        for (var index = 0; index < thresholds.Outcomes.Count; index++)
        {
            var outcome = thresholds.Outcomes[index];
            ArgumentNullException.ThrowIfNull(outcome);
            outcomes[index] = outcome with { };
        }

        return new ExperimentDeterministicPolicyEvidence(
            runEvaluationName,
            new EvaluationThresholdResult
            {
                Decision = thresholds.Decision,
                Outcomes = Array.AsReadOnly(outcomes),
            },
            unavailableReason: null);
    }

    /// <summary>Creates evidence explaining why threshold metrics were unavailable.</summary>
    /// <param name="runEvaluationName">The run-evaluator name expected to supply metrics.</param>
    /// <param name="unavailableReason">The stable reason threshold metrics were unavailable.</param>
    /// <returns>Unavailable deterministic policy evidence.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="runEvaluationName"/> or <paramref name="unavailableReason"/> is empty or
    /// consists only of white-space characters.
    /// </exception>
    public static ExperimentDeterministicPolicyEvidence Unavailable(
        string runEvaluationName,
        string unavailableReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runEvaluationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(unavailableReason);
        return new ExperimentDeterministicPolicyEvidence(
            runEvaluationName,
            thresholds: null,
            unavailableReason);
    }
}
