namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Configures runtime behavior for a <see cref="PipelinePhase"/>, including
/// async lifecycle hooks and an optional phase-level token budget.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally distinct from <see cref="StageExecutionPolicy"/>. Phase
/// policies control cross-stage concerns (budget scope, workspace reconfiguration),
/// while stage policies control per-stage behavior (skip, retry, validation).
/// </para>
/// <para>
/// <see cref="OnEnterAsync"/> fires before any stage pre-work in the phase — before
/// <see cref="StageExecutionPolicy.ShouldSkip"/> evaluation, before prompt construction,
/// before executor invocation. This guarantees that workspace/state configuration is
/// applied before any stage can observe it.
/// </para>
/// <para>
/// <see cref="OnExitAsync"/> fires after the last stage completes (or on phase failure),
/// in a <c>finally</c> block. It always runs regardless of success or failure, enabling
/// cleanup and summary logic.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var policy = new PipelinePhasePolicy
/// {
///     OnEnterAsync = (ctx, ct) =>
///     {
///         Console.WriteLine($"Entering phase: {ctx.PhaseName}");
///         return ValueTask.CompletedTask;
///     },
///     OnExitAsync = (ctx, ct) =>
///     {
///         Console.WriteLine($"Exiting phase: {ctx.PhaseName}");
///         return ValueTask.CompletedTask;
///     },
///     TokenBudget = 50_000,
/// };
/// </code>
/// </example>
public sealed record PipelinePhasePolicy
{
    /// <summary>
    /// Async callback invoked before the first stage in the phase executes.
    /// Fires before any stage pre-work (<see cref="StageExecutionPolicy.ShouldSkip"/>,
    /// prompt construction, executor invocation). Fires even if all stages in the
    /// phase will be skipped. Not called if the phase has zero stages and no callback is set.
    /// </summary>
    public Func<PhaseContext, CancellationToken, ValueTask>? OnEnterAsync { get; init; }

    /// <summary>
    /// Async callback invoked after the last stage in the phase completes (or on
    /// phase failure/cancellation). Runs in a <c>finally</c> block — always fires
    /// regardless of success or failure.
    /// </summary>
    public Func<PhaseContext, CancellationToken, ValueTask>? OnExitAsync { get; init; }

    /// <summary>
    /// Optional phase-level token budget. When set, the runner scopes a budget
    /// tracker for the entire phase. Individual stage budgets
    /// (<see cref="StageExecutionPolicy.TokenBudget"/>) create child scopes within
    /// the phase budget scope.
    /// </summary>
    public long? TokenBudget { get; init; }
}
