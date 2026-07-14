namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies execution outcomes that a retry policy may select.
/// </summary>
[Flags]
public enum ExperimentRetryableOutcome
{
    /// <summary>No execution outcome is retried.</summary>
    None = 0,

    /// <summary>Retry task execution failures.</summary>
    ExecutionFailure = 1 << 0,

    /// <summary>Retry attempt timeouts.</summary>
    Timeout = 1 << 1,

    /// <summary>Retry task-originated cancellation.</summary>
    TaskCancellation = 1 << 2,
}
