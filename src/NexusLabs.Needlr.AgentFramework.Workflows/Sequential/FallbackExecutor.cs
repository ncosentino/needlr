namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Tries a primary executor and, on failure, falls back to a secondary executor.
/// User cancellation is never swallowed.
/// </summary>
/// <param name="primary">The preferred executor to try first.</param>
/// <param name="fallback">The executor to use if the primary throws.</param>
/// <param name="shouldFallback">
/// Optional predicate controlling which exceptions trigger fallback. When <see langword="null"/>
/// (the default), any non-cancellation exception triggers fallback. When provided, only exceptions
/// where the predicate returns <see langword="true"/> trigger fallback; others propagate.
/// </param>
/// <example>
/// <code>
/// // Default — falls back on any failure
/// var executor = new FallbackExecutor(primaryExecutor, fallbackExecutor);
///
/// // Narrow — only fall back on timeouts
/// var executor = new FallbackExecutor(primaryExecutor, fallbackExecutor,
///     shouldFallback: ex =&gt; ex is TaskCanceledException or HttpRequestException);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class FallbackExecutor(
    IStageExecutor primary,
    IStageExecutor fallback,
    Func<Exception, bool>? shouldFallback = null) : IStageExecutor
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
        catch (OperationCanceledException) when (context.CallerCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (shouldFallback is null || shouldFallback(ex))
        {
            return await fallback.ExecuteAsync(context, cancellationToken);
        }
    }
}
