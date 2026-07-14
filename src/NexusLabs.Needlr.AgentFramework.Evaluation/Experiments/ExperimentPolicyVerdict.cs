namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a policy's decision and evidence before runner-owned result identity is applied.
/// </summary>
public sealed class ExperimentPolicyVerdict
{
    /// <summary>Gets the policy decision.</summary>
    public required EvaluationDecision Decision { get; init; }

    /// <summary>Gets deterministic threshold evidence, when applicable.</summary>
    public ExperimentDeterministicPolicyEvidence? DeterministicEvidence { get; init; }

    /// <summary>Gets binary statistical evidence, when applicable.</summary>
    public ExperimentBinaryStatisticalEvidence? StatisticalEvidence { get; init; }
}
