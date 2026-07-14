namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the aggregate decision for an experiment run.
/// </summary>
public enum ExperimentRunDecision
{
    /// <summary>Every required policy passed.</summary>
    Passed,

    /// <summary>At least one required policy failed.</summary>
    Failed,

    /// <summary>No required policy failed, but at least one was inconclusive.</summary>
    Inconclusive,

    /// <summary>No required policy was configured.</summary>
    NotEvaluated,
}
