namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the terminal status of one run evaluator.
/// </summary>
public enum ExperimentRunEvaluationStatus
{
    /// <summary>The evaluator returned and its metrics were normalized.</summary>
    Succeeded,

    /// <summary>The evaluator or metric normalization failed.</summary>
    Failed,
}
