namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the experiment stage that produced a structured failure.
/// </summary>
public enum ExperimentFailureStage
{
    /// <summary>Task execution produced the failure.</summary>
    Execution,

    /// <summary>Item evaluation produced the failure.</summary>
    ItemEvaluation,
}
