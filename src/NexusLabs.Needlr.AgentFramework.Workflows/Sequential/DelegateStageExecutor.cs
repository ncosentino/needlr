namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Executes a pipeline stage by invoking a caller-provided delegate.
/// Use for programmatic stages that do not involve an AI agent.
/// </summary>
/// <param name="step">The async delegate to execute for this stage.</param>
/// <example>
/// <code>
/// var executor = new DelegateStageExecutor(async (ctx, ct) =>
/// {
///     var content = ctx.Workspace.TryReadFile("draft.md");
///     // Transform content...
///     ctx.Workspace.TryWriteFile("final.md", "transformed content");
/// });
///
/// var result = await executor.ExecuteAsync(context, cancellationToken);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class DelegateStageExecutor(
    Func<StageExecutionContext, CancellationToken, Task> step) : IStageExecutor
{
    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        await step(context, cancellationToken);
        return StageExecutionResult.Success(context.StageName, diagnostics: null, responseText: null);
    }
}
