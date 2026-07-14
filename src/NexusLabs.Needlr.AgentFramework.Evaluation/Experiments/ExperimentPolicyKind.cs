namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the evidence model used by an experiment policy.
/// </summary>
public enum ExperimentPolicyKind
{
    /// <summary>The policy applies deterministic rules to measured evidence.</summary>
    Deterministic,

    /// <summary>The policy applies a statistical decision rule.</summary>
    Statistical,
}
