namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Tries a primary executor and, on failure, falls back to a secondary executor.
/// Cancellation is never swallowed.
/// </summary>
/// <param name="primary">The preferred executor to try first.</param>
/// <param name="fallback">The executor to use if the primary throws.</param>
/// <example>
/// <code>
/// var executor = new FallbackExecutor(
///     new AgentStageExecutor(gpt4Agent, promptFactory),
///     new AgentStageExecutor(gpt35Agent, promptFactory));
///
/// var result = await executor.ExecuteAsync(context, cancellationToken);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class FallbackExecutor(
    IStageExecutor primary,
    IStageExecutor fallback) : IStageExecutor
{
    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await primary.ExecuteAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return await fallback.ExecuteAsync(context, cancellationToken);
        }
    }
}
