namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Describes why an <see cref="IIterativeAgentLoop"/> run terminated.
/// </summary>
public enum TerminationReason
{
    /// <summary>
    /// The <see cref="IterativeLoopOptions.IsComplete"/> predicate returned
    /// <see langword="true"/> after an iteration — the agent achieved its goal.
    /// </summary>
    Completed,

    /// <summary>
    /// The model produced a text response without requesting tool calls,
    /// signaling natural completion of the task.
    /// </summary>
    NaturalCompletion,

    /// <summary>
    /// The loop exhausted <see cref="IterativeLoopOptions.MaxIterations"/>
    /// without the <see cref="IterativeLoopOptions.IsComplete"/> predicate
    /// returning <see langword="true"/>.
    /// </summary>
    MaxIterationsReached,

    /// <summary>
    /// The cumulative tool call count across all iterations exceeded
    /// <see cref="IterativeLoopOptions.MaxTotalToolCalls"/>.
    /// </summary>
    MaxToolCallsReached,

    /// <summary>
    /// The <see cref="Budget.ITokenBudgetTracker"/> reported usage above the
    /// budget pressure threshold, and the loop ran one final finalization
    /// iteration before terminating.
    /// </summary>
    BudgetPressure,

    /// <summary>
    /// The loop was cancelled via <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    Cancelled,

    /// <summary>
    /// An unrecoverable error occurred (prompt factory failure, LLM exception, etc.).
    /// </summary>
    Error,

    /// <summary>
    /// The loop detected that consecutive iterations produced nearly identical
    /// token usage, indicating the LLM is repeating the same work without
    /// making progress. Controlled by
    /// <see cref="IterativeLoopOptions.StallDetection"/>.
    /// </summary>
    StallDetected,
}
