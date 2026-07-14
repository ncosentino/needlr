namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies a stable experiment failure category.
/// </summary>
public enum ExperimentFailureCode
{
    /// <summary>Task execution threw an exception.</summary>
    ExecutionFailed,

    /// <summary>The attempt deadline was requested before execution completed.</summary>
    AttemptTimedOut,

    /// <summary>The task canceled independently of caller cancellation and timeout.</summary>
    TaskCanceled,

    /// <summary>Item evaluation threw an exception.</summary>
    EvaluationFailed,
}
