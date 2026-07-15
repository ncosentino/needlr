namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Coordinates Langfuse resource creation within one process.
/// </summary>
/// <remarks>
/// Use a distributed <see cref="ILangfuseResourceLockProvider"/> when multiple application
/// processes can initialize the same Langfuse project concurrently.
/// </remarks>
[DoNotAutoRegister]
public sealed class LangfuseInProcessResourceLockProvider : ILangfuseResourceLockProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<string, LockEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes an empty in-process resource lock provider.
    /// </summary>
    public LangfuseInProcessResourceLockProvider()
    {
    }

    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> AcquireAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        LockEntry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new LockEntry();
                _entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new LockLease(this, key, entry);
        }
        catch (OperationCanceledException)
        {
            RemoveReference(key, entry);
            throw;
        }
    }

    private void Release(string key, LockEntry entry)
    {
        entry.Semaphore.Release();
        RemoveReference(key, entry);
    }

    private void RemoveReference(string key, LockEntry entry)
    {
        lock (_sync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0
                && _entries.TryGetValue(key, out var current)
                && ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed class LockLease(
        LangfuseInProcessResourceLockProvider owner,
        string key,
        LockEntry entry)
        : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Release(key, entry);
            }

            return ValueTask.CompletedTask;
        }
    }
}
