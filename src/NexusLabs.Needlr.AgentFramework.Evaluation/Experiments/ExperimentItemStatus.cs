namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes the terminal quality-processing state of one experiment case trial.
/// </summary>
public enum ExperimentItemStatus
{
    /// <summary>Execution and optional item evaluation succeeded.</summary>
    Succeeded,

    /// <summary>The task threw a non-cancellation exception.</summary>
    ExecutionFailed,

    /// <summary>The task attempt exceeded its cooperative deadline.</summary>
    TimedOut,

    /// <summary>The task canceled independently of caller cancellation and timeout.</summary>
    Canceled,

    /// <summary>Execution succeeded but item evaluation failed.</summary>
    EvaluationFailed,

    /// <summary>An explicitly required item-scope prerequisite prevented task execution.</summary>
    PrerequisiteFailed,
}
