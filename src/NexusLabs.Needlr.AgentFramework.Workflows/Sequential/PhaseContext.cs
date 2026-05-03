using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Provides typed access to pipeline state during phase lifecycle hooks
/// (<see cref="PipelinePhasePolicy.OnEnterAsync"/> and
/// <see cref="PipelinePhasePolicy.OnExitAsync"/>).
/// </summary>
/// <param name="PhaseName">Human-readable name of the current phase.</param>
/// <param name="PhaseIndex">Zero-based index of the current phase in the pipeline.</param>
/// <param name="TotalPhases">Total number of phases in the pipeline.</param>
/// <param name="Workspace">The shared workspace for the pipeline.</param>
/// <param name="PipelineState">
/// Optional shared state object passed to all phases. Use
/// <see cref="GetRequiredState{T}"/> for type-safe access.
/// </param>
/// <example>
/// <code>
/// var policy = new PipelinePhasePolicy
/// {
///     OnEnterAsync = (ctx, ct) =>
///     {
///         var state = ctx.GetRequiredState&lt;MyPipelineState&gt;();
///         Console.WriteLine($"Phase {ctx.PhaseIndex + 1}/{ctx.TotalPhases}: {ctx.PhaseName}");
///         return ValueTask.CompletedTask;
///     },
/// };
/// </code>
/// </example>
public sealed record PhaseContext(
    string PhaseName,
    int PhaseIndex,
    int TotalPhases,
    IWorkspace Workspace,
    object? PipelineState = null)
{
    /// <summary>
    /// Gets the typed pipeline state, or throws if no state was provided or the type doesn't match.
    /// </summary>
    /// <typeparam name="T">The expected pipeline state type.</typeparam>
    /// <returns>The pipeline state cast to <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="PipelineState"/> is <see langword="null"/> or not of type <typeparamref name="T"/>.
    /// </exception>
    public T GetRequiredState<T>() where T : class =>
        PipelineState as T ?? throw new InvalidOperationException(
            $"Pipeline state is not available or is not of type {typeof(T).Name}.");
}
