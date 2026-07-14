using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

internal sealed class RecordingExperimentConcurrencyLimiter :
    IExperimentConcurrencyLimiter,
    IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private int _acquisitionCount;
    private int _activeLeaseCount;
    private int _disposeCount;
    private int _maximumActiveLeaseCount;

    public RecordingExperimentConcurrencyLimiter(int maximumConcurrency)
    {
        _semaphore = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    }

    public int AcquisitionCount => Volatile.Read(ref _acquisitionCount);

    public int ActiveLeaseCount => Volatile.Read(ref _activeLeaseCount);

    public int MaximumActiveLeaseCount => Volatile.Read(ref _maximumActiveLeaseCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _acquisitionCount);
        await _semaphore.WaitAsync(cancellationToken);
        var active = Interlocked.Increment(ref _activeLeaseCount);
        UpdateMaximum(ref _maximumActiveLeaseCount, active);
        return new Lease(this);
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }

    private void Release()
    {
        Interlocked.Decrement(ref _activeLeaseCount);
        _semaphore.Release();
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (candidate <= current
                || Interlocked.CompareExchange(ref maximum, candidate, current) == current)
            {
                return;
            }
        }
    }

    private sealed class Lease(RecordingExperimentConcurrencyLimiter owner) :
        IAsyncDisposable
    {
        private RecordingExperimentConcurrencyLimiter? _owner = owner;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
