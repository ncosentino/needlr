namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Defines the execution strategy for a single pipeline stage.
/// Implementations include agent-driven stages, programmatic/delegate stages,
/// and critique-and-revise loops.
/// </summary>
/// <example>
/// <code>
/// public class MyCustomExecutor : IStageExecutor
/// {
///     public async Task&lt;StageExecutionResult&gt; ExecuteAsync(
///         StageExecutionContext context,
///         CancellationToken cancellationToken)
///     {
///         // Custom stage logic here
///         return StageExecutionResult.Success(context.StageName, diagnostics: null, responseText: "done");
///     }
/// }
/// </code>
/// </example>
public interface IStageExecutor
{
    /// <summary>
    /// Executes the stage logic and returns a result indicating success or failure.
    /// </summary>
    /// <param name="context">The execution context providing access to shared pipeline state.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A <see cref="StageExecutionResult"/> describing the outcome.</returns>
    Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken);
}
