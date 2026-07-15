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

    /// <summary>The retry policy failed while selecting the next attempt.</summary>
    RetryPolicyFailed,

    /// <summary>A run evaluator or its metric normalization failed.</summary>
    RunEvaluationFailed,

    /// <summary>An experiment policy failed.</summary>
    PolicyFailed,

    /// <summary>An item scope failed during entry, activation, completion, or disposal.</summary>
    ItemScopeFailed,

    /// <summary>An explicitly required item scope prevented the next task attempt.</summary>
    ItemScopePrerequisiteFailed,
}
