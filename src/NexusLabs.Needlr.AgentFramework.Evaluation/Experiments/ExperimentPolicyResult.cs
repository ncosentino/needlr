namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one isolated experiment policy outcome.
/// </summary>
public sealed record ExperimentPolicyResult
{
    /// <summary>Gets the stable policy name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the policy kind.</summary>
    public required ExperimentPolicyKind Kind { get; init; }

    /// <summary>Gets a value indicating whether the policy contributes to the run decision.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Gets the policy decision.</summary>
    public required EvaluationDecision Decision { get; init; }

    /// <summary>Gets deterministic threshold evidence, when applicable.</summary>
    public ExperimentDeterministicPolicyEvidence? DeterministicEvidence { get; init; }

    /// <summary>Gets binary statistical evidence, when applicable.</summary>
    public ExperimentBinaryStatisticalEvidence? StatisticalEvidence { get; init; }

    /// <summary>Gets the structured failure when policy execution failed.</summary>
    public ExperimentFailure? Failure { get; init; }
}
