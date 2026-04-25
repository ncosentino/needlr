namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Wraps an inner executor, catching exceptions and returning a failed result
/// instead of throwing. This enables "advisory" stage behavior where a failure
/// does not halt the pipeline.
/// </summary>
/// <param name="inner">The executor to wrap.</param>
/// <param name="onFailure">Optional callback invoked when the inner executor throws.</param>
/// <example>
/// <code>
/// var safeExecutor = new ContinueOnFailureExecutor(
///     innerExecutor,
///     ex => logger.LogWarning(ex, "Stage failed but continuing"));
///
/// var result = await safeExecutor.ExecuteAsync(context, cancellationToken);
/// // result.Succeeded == false if the inner executor threw, but no exception propagates.
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class ContinueOnFailureExecutor(
    IStageExecutor inner,
    Action<Exception>? onFailure = null) : IStageExecutor
{
    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await inner.ExecuteAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            onFailure?.Invoke(ex);
            return StageExecutionResult.Failed(context.StageName, ex);
        }
    }
}
