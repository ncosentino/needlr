namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes the terminal state of one operational experiment attempt.
/// </summary>
public enum ExperimentAttemptStatus
{
    /// <summary>The task returned an output before cancellation or timeout.</summary>
    Succeeded,

    /// <summary>The task threw a non-cancellation exception.</summary>
    Failed,

    /// <summary>The attempt deadline was requested while the caller token remained active.</summary>
    TimedOut,

    /// <summary>The task canceled without caller cancellation or an attempt deadline.</summary>
    Canceled,
}
