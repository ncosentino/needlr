namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Configures runtime behavior for a single pipeline stage, including
/// conditional skipping, post-execution validation with retries, and
/// per-stage token budgets.
/// </summary>
/// <example>
/// <code>
/// var policy = new StageExecutionPolicy
/// {
///     ShouldSkip = ctx => ctx.StageIndex > 3,
///     PostValidation = result => result.ResponseText?.Contains("error") == true
///         ? "Response contained an error"
///         : null,
///     MaxAttempts = 3,
///     TokenBudget = 5000,
/// };
/// </code>
/// </example>
public sealed record StageExecutionPolicy
{
    /// <summary>
    /// Evaluated at runtime. When true, the stage is skipped entirely.
    /// </summary>
    public Func<StageExecutionContext, bool>? ShouldSkip { get; init; }

    /// <summary>
    /// Called after stage execution. Returns <see langword="null"/> on success,
    /// or an error message that triggers a retry (up to <see cref="MaxAttempts"/>).
    /// </summary>
    public Func<StageExecutionResult, string?>? PostValidation { get; init; }

    /// <summary>
    /// Maximum execution attempts for post-validation retries. Default is 1 (no retry).
    /// </summary>
    public int MaxAttempts { get; init; } = 1;

    /// <summary>
    /// Optional per-stage token budget. When set, the runner scopes a child
    /// budget tracker for this stage.
    /// </summary>
    public long? TokenBudget { get; init; }

    /// <summary>
    /// Called after stage execution (success or failure, not skip).
    /// Receives the execution result and context for workspace checks or state updates.
    /// </summary>
    public Func<StageExecutionResult, StageExecutionContext, Task>? AfterExecution { get; init; }
}
