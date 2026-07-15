namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a semaphore-backed concurrency limiter suitable for sharing through dependency
/// injection.
/// </summary>
[DoNotAutoRegister]
public sealed class ExperimentConcurrencyLimiter :
    IExperimentConcurrencyLimiter,
    IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Initializes a concurrency limiter.
    /// </summary>
    /// <param name="maximumConcurrency">The maximum number of simultaneously active leases.</param>
    public ExperimentConcurrencyLimiter(int maximumConcurrency)
    {
        if (maximumConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumConcurrency),
                maximumConcurrency,
                "The maximum concurrency must be positive.");
        }

        _semaphore = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    }

    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_semaphore);
    }

    /// <summary>
    /// Releases resources owned by the limiter.
    /// </summary>
    /// <returns>A completed disposal operation.</returns>
    public ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
