using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Provides typed access to the pipeline's shared state during stage execution.
/// </summary>
/// <param name="Workspace">The shared workspace for reading/writing files across stages.</param>
/// <param name="DiagnosticsAccessor">Accessor for capturing per-stage diagnostics.</param>
/// <param name="ProgressReporter">Optional progress reporter for emitting pipeline events.</param>
/// <param name="StageIndex">Zero-based index of the current stage in the pipeline.</param>
/// <param name="TotalStages">Total number of stages in the pipeline.</param>
/// <param name="StageName">Human-readable name of the current stage.</param>
/// <param name="CallerCancellationToken">
/// The original caller's cancellation token. Decorators that create linked tokens
/// (e.g. <see cref="TimeoutExecutor"/>) should check this to distinguish user
/// cancellation from internal timeouts.
/// </param>
/// <param name="PipelineState">
/// Optional shared state object passed to all stages in the pipeline. Use
/// <see cref="GetRequiredState{T}"/> to retrieve it with type safety.
/// </param>
/// <example>
/// <code>
/// public async Task&lt;StageExecutionResult&gt; ExecuteAsync(
///     StageExecutionContext context,
///     CancellationToken cancellationToken)
/// {
///     Console.WriteLine($"Running stage {context.StageIndex + 1}/{context.TotalStages}: {context.StageName}");
///     return StageExecutionResult.Success(context.StageName, diagnostics: null, responseText: null);
/// }
/// </code>
/// </example>
public sealed record StageExecutionContext(
    IWorkspace Workspace,
    IAgentDiagnosticsAccessor DiagnosticsAccessor,
    IProgressReporter? ProgressReporter,
    int StageIndex,
    int TotalStages,
    string StageName,
    CancellationToken CallerCancellationToken = default,
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
