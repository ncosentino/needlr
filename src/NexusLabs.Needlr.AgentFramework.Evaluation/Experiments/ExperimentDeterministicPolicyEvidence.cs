namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes deterministic threshold evidence from one named run evaluation.
/// </summary>
public sealed record ExperimentDeterministicPolicyEvidence
{
    /// <summary>Gets the run-evaluator name that supplied metrics.</summary>
    public required string RunEvaluationName { get; init; }

    /// <summary>Gets the structured threshold result when metrics were available.</summary>
    public EvaluationThresholdResult? Thresholds { get; init; }

    /// <summary>Gets why the named run evaluation could not supply metrics.</summary>
    public string? UnavailableReason { get; init; }
}
