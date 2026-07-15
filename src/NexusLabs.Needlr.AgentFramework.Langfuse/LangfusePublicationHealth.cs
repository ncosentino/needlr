namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Collects thread-safe Langfuse publication-health counters.
/// </summary>
[DoNotAutoRegister]
public sealed class LangfusePublicationHealth
{
    private readonly bool _isEnabled;
    private readonly object _drainSync = new();
    private long _traceObserved;
    private long _traceEnqueued;
    private long _traceDropped;
    private long _traceAcknowledged;
    private long _traceFailed;
    private long _traceSuccessfulBatches;
    private long _traceFailedBatches;
    private long _scoreInFlight;
    private long _scoreSucceeded;
    private long _scoreFailed;
    private long _scoreCanceled;
    private long _linkInFlight;
    private long _linkSucceeded;
    private long _linkFailed;
    private long _linkCanceled;
    private long _retryRateLimited;
    private long _retryTransientServer;
    private long _retryTimedOut;
    private long _retryTransport;
    private long _drainAttempts;
    private LangfuseDrainStatus _drainStatus;
    private TimeSpan? _drainDuration;

    /// <summary>
    /// Initializes publication-health tracking.
    /// </summary>
    /// <param name="isEnabled">Whether the associated Langfuse integration is enabled.</param>
    public LangfusePublicationHealth(bool isEnabled)
    {
        _isEnabled = isEnabled;
        _drainStatus = isEnabled
            ? LangfuseDrainStatus.NotAttempted
            : LangfuseDrainStatus.Disabled;
    }

    /// <summary>
    /// Gets an immutable snapshot of all observed publication-health counters.
    /// </summary>
    /// <returns>The current publication-health snapshot.</returns>
    public LangfusePublicationHealthSnapshot GetSnapshot()
    {
        LangfuseDrainHealth drain;
        lock (_drainSync)
        {
            drain = new LangfuseDrainHealth(
                _drainAttempts,
                _drainStatus,
                _drainDuration);
        }

        return new LangfusePublicationHealthSnapshot(
            _isEnabled,
            new LangfuseTraceExportHealth(
                Volatile.Read(ref _traceObserved),
                Volatile.Read(ref _traceEnqueued),
                Volatile.Read(ref _traceDropped),
                Volatile.Read(ref _traceAcknowledged),
                Volatile.Read(ref _traceFailed),
                Volatile.Read(ref _traceSuccessfulBatches),
                Volatile.Read(ref _traceFailedBatches)),
            new LangfusePublicationOperationHealth(
                Volatile.Read(ref _scoreInFlight),
                Volatile.Read(ref _scoreSucceeded),
                Volatile.Read(ref _scoreFailed),
                Volatile.Read(ref _scoreCanceled)),
            new LangfusePublicationOperationHealth(
                Volatile.Read(ref _linkInFlight),
                Volatile.Read(ref _linkSucceeded),
                Volatile.Read(ref _linkFailed),
                Volatile.Read(ref _linkCanceled)),
            new LangfuseRetryHealth(
                Volatile.Read(ref _retryRateLimited),
                Volatile.Read(ref _retryTransientServer),
                Volatile.Read(ref _retryTimedOut),
                Volatile.Read(ref _retryTransport)),
            drain);
    }

    internal void RecordTraceObserved() => Interlocked.Increment(ref _traceObserved);

    internal void RecordTraceEnqueued() => Interlocked.Increment(ref _traceEnqueued);

    internal void RecordTraceDropped() => Interlocked.Increment(ref _traceDropped);

    internal void RecordTraceExport(long count, bool succeeded)
    {
        if (succeeded)
        {
            Interlocked.Add(ref _traceAcknowledged, count);
            Interlocked.Increment(ref _traceSuccessfulBatches);
        }
        else
        {
            Interlocked.Add(ref _traceFailed, count);
            Interlocked.Increment(ref _traceFailedBatches);
        }
    }

    internal void BeginScoreUpload() => Interlocked.Increment(ref _scoreInFlight);

    internal void CompleteScoreUpload(bool succeeded)
    {
        Interlocked.Decrement(ref _scoreInFlight);
        if (succeeded)
        {
            Interlocked.Increment(ref _scoreSucceeded);
        }
        else
        {
            Interlocked.Increment(ref _scoreFailed);
        }
    }

    internal void CancelScoreUpload()
    {
        Interlocked.Decrement(ref _scoreInFlight);
        Interlocked.Increment(ref _scoreCanceled);
    }

    internal void BeginItemLink() => Interlocked.Increment(ref _linkInFlight);

    internal void CompleteItemLink(bool succeeded)
    {
        Interlocked.Decrement(ref _linkInFlight);
        if (succeeded)
        {
            Interlocked.Increment(ref _linkSucceeded);
        }
        else
        {
            Interlocked.Increment(ref _linkFailed);
        }
    }

    internal void CancelItemLink()
    {
        Interlocked.Decrement(ref _linkInFlight);
        Interlocked.Increment(ref _linkCanceled);
    }

    internal void RecordRetry(LangfuseRetryReason reason)
    {
        switch (reason)
        {
            case LangfuseRetryReason.RateLimited:
                Interlocked.Increment(ref _retryRateLimited);
                break;
            case LangfuseRetryReason.TransientServer:
                Interlocked.Increment(ref _retryTransientServer);
                break;
            case LangfuseRetryReason.TimedOut:
                Interlocked.Increment(ref _retryTimedOut);
                break;
            case LangfuseRetryReason.Transport:
                Interlocked.Increment(ref _retryTransport);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "The Langfuse retry reason is not defined.");
        }
    }

    internal void BeginDrain()
    {
        lock (_drainSync)
        {
            _drainAttempts++;
            _drainStatus = LangfuseDrainStatus.InProgress;
            _drainDuration = null;
        }
    }

    internal void CompleteDrain(bool completed, TimeSpan duration)
    {
        lock (_drainSync)
        {
            _drainStatus = completed
                ? LangfuseDrainStatus.Completed
                : LangfuseDrainStatus.Incomplete;
            _drainDuration = duration;
        }
    }
}
