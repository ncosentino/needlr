namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a policy's decision and evidence before runner-owned result identity is applied.
/// </summary>
public sealed record ExperimentPolicyVerdict
{
    private ExperimentPolicyVerdict(
        EvaluationDecision decision,
        ExperimentDeterministicPolicyEvidence? deterministicEvidence,
        ExperimentBinaryStatisticalEvidence? statisticalEvidence)
    {
        Decision = decision;
        DeterministicEvidence = deterministicEvidence;
        StatisticalEvidence = statisticalEvidence;
    }

    /// <summary>Gets the policy decision.</summary>
    public EvaluationDecision Decision { get; }

    /// <summary>Gets deterministic threshold evidence, when applicable.</summary>
    public ExperimentDeterministicPolicyEvidence? DeterministicEvidence { get; }

    /// <summary>Gets binary statistical evidence, when applicable.</summary>
    public ExperimentBinaryStatisticalEvidence? StatisticalEvidence { get; }

    /// <summary>Creates a policy verdict without structured evidence.</summary>
    /// <param name="decision">The policy decision.</param>
    /// <returns>A verdict without structured evidence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="decision"/> is not defined.
    /// </exception>
    public static ExperimentPolicyVerdict WithoutEvidence(EvaluationDecision decision)
    {
        ValidateDecision(decision);
        return new ExperimentPolicyVerdict(
            decision,
            deterministicEvidence: null,
            statisticalEvidence: null);
    }

    /// <summary>Creates a policy verdict from deterministic threshold evidence.</summary>
    /// <param name="decision">The policy decision.</param>
    /// <param name="evidence">The deterministic threshold evidence.</param>
    /// <returns>A verdict containing deterministic evidence.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="evidence"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="decision"/> is not defined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Available threshold evidence has a different decision.
    /// </exception>
    public static ExperimentPolicyVerdict FromDeterministicEvidence(
        EvaluationDecision decision,
        ExperimentDeterministicPolicyEvidence evidence)
    {
        ValidateDecision(decision);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Thresholds is { } thresholds
            && thresholds.Decision != decision)
        {
            throw new ArgumentException(
                "The deterministic evidence decision must match the policy decision.",
                nameof(evidence));
        }

        return new ExperimentPolicyVerdict(
            decision,
            evidence,
            statisticalEvidence: null);
    }

    /// <summary>Creates a policy verdict from binary statistical evidence.</summary>
    /// <param name="evidence">The binary statistical evidence.</param>
    /// <returns>A verdict whose decision is derived from the evidence.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="evidence"/> is <see langword="null"/>.
    /// </exception>
    public static ExperimentPolicyVerdict FromStatisticalEvidence(
        ExperimentBinaryStatisticalEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var decision =
            evidence.UnknownSampleTreatment == ExperimentUnknownSampleTreatment.Inconclusive
            && evidence.ExclusionCount > 0
                ? EvaluationDecision.Inconclusive
                : evidence.SampleCount < evidence.MinimumSampleCount
                    ? EvaluationDecision.Inconclusive
                    : evidence.OneSidedLowerBound >= evidence.RequiredSuccessRate
                        ? EvaluationDecision.Passed
                        : evidence.OneSidedUpperBound < evidence.RequiredSuccessRate
                            ? EvaluationDecision.Failed
                            : EvaluationDecision.Inconclusive;
        return new ExperimentPolicyVerdict(
            decision,
            deterministicEvidence: null,
            evidence);
    }

    private static void ValidateDecision(EvaluationDecision decision)
    {
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "The policy decision is not defined.");
        }
    }
}
