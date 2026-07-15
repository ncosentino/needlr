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

    /// <summary>A run evaluator produced the failure.</summary>
    RunEvaluation,

    /// <summary>A retry or quality policy produced the failure.</summary>
    Policy,

    /// <summary>An item scope or final result sink produced the failure.</summary>
    Publication,
}
