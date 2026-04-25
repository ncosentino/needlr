namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Wraps an executor with a maximum execution duration. If the inner executor
/// does not complete within the specified timeout, the linked cancellation token
/// is triggered.
/// </summary>
/// <param name="inner">The executor to wrap.</param>
/// <param name="timeout">The maximum duration before cancellation.</param>
/// <example>
/// <code>
/// var executor = new TimeoutExecutor(innerExecutor, TimeSpan.FromSeconds(30));
///
/// // Throws OperationCanceledException if inner takes longer than 30 seconds.
/// var result = await executor.ExecuteAsync(context, cancellationToken);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class TimeoutExecutor(
    IStageExecutor inner,
    TimeSpan timeout) : IStageExecutor
{
    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        return await inner.ExecuteAsync(context, cts.Token);
    }
}
