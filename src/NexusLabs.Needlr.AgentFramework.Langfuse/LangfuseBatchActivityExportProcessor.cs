using System.Collections.Concurrent;
using System.Diagnostics;

using OpenTelemetry;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Batches recorded activities while exposing exact local queue and exporter outcomes.
/// </summary>
internal sealed class LangfuseBatchActivityExportProcessor : BaseExportProcessor<Activity>
{
    private readonly ConcurrentQueue<Activity> _queue = new();
    private readonly AutoResetEvent _signal = new(initialState: false);
    private readonly object _disposeSync = new();
    private readonly object _progressSync = new();
    private readonly LangfusePublicationHealth _health;
    private readonly int _maxQueueSize;
    private readonly int _scheduledDelayMilliseconds;
    private readonly int _maxExportBatchSize;
    private readonly Thread _worker;
    private long _enqueuedCount;
    private long _processedCount;
    private int _queueCount;
    private int _forceFlushWaiters;
    private int _shutdownRequested;
    private int _disposeRequested;
    private int _resourcesDisposed;
    private bool _workerExited;

    public LangfuseBatchActivityExportProcessor(
        BaseExporter<Activity> exporter,
        LangfusePublicationHealth health,
        int maxQueueSize,
        int scheduledDelayMilliseconds,
        int maxExportBatchSize)
        : base(exporter)
    {
        ArgumentNullException.ThrowIfNull(health);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxQueueSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(scheduledDelayMilliseconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxExportBatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxExportBatchSize, maxQueueSize);

        _health = health;
        _maxQueueSize = maxQueueSize;
        _scheduledDelayMilliseconds = scheduledDelayMilliseconds;
        _maxExportBatchSize = maxExportBatchSize;
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "Needlr Langfuse trace exporter",
        };
        _worker.Start();
    }

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Recorded)
        {
            base.OnEnd(data);
        }
    }

    protected override void OnExport(Activity data)
    {
        _health.RecordTraceObserved();
        if (Volatile.Read(ref _shutdownRequested) != 0 || !TryReserveQueueSlot())
        {
            _health.RecordTraceDropped();
            return;
        }

        _queue.Enqueue(data);
        Interlocked.Increment(ref _enqueuedCount);
        _health.RecordTraceEnqueued();
        _signal.Set();
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        var target = Volatile.Read(ref _enqueuedCount);
        if (Volatile.Read(ref _processedCount) >= target)
        {
            return true;
        }

        Interlocked.Increment(ref _forceFlushWaiters);
        _signal.Set();
        try
        {
            return WaitForProcessedCount(target, timeoutMilliseconds);
        }
        finally
        {
            Interlocked.Decrement(ref _forceFlushWaiters);
        }
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Interlocked.Exchange(ref _shutdownRequested, 1);
        _signal.Set();

        var stopwatch = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();
        var workerCompleted = timeoutMilliseconds == Timeout.Infinite
            ? JoinIndefinitely()
            : _worker.Join(timeoutMilliseconds);
        if (!workerCompleted)
        {
            return false;
        }

        return exporter.Shutdown(GetRemainingMilliseconds(timeoutMilliseconds, stopwatch));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Exchange(ref _disposeRequested, 1);
            Interlocked.Exchange(ref _shutdownRequested, 1);
            _signal.Set();
            lock (_disposeSync)
            {
                if (_workerExited)
                {
                    DisposeResources();
                }
            }
        }
    }

    private bool TryReserveQueueSlot()
    {
        while (true)
        {
            var count = Volatile.Read(ref _queueCount);
            if (count >= _maxQueueSize)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _queueCount, count + 1, count) == count)
            {
                return true;
            }
        }
    }

    private void WorkerLoop()
    {
        try
        {
            while (true)
            {
                if (Volatile.Read(ref _queueCount) == 0)
                {
                    if (Volatile.Read(ref _shutdownRequested) != 0)
                    {
                        return;
                    }

                    _signal.WaitOne();
                    continue;
                }

                WaitForExportWindow();
                ExportBatch();
            }
        }
        finally
        {
            lock (_progressSync)
            {
                Monitor.PulseAll(_progressSync);
            }

            lock (_disposeSync)
            {
                _workerExited = true;
                if (Volatile.Read(ref _disposeRequested) != 0)
                {
                    DisposeResources();
                }
            }
        }
    }

    private void WaitForExportWindow()
    {
        if (ShouldExportImmediately())
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        while (!ShouldExportImmediately())
        {
            var remaining = _scheduledDelayMilliseconds
                - (int)Math.Ceiling(stopwatch.Elapsed.TotalMilliseconds);
            if (remaining <= 0)
            {
                return;
            }

            _signal.WaitOne(remaining);
        }
    }

    private bool ShouldExportImmediately() =>
        Volatile.Read(ref _shutdownRequested) != 0
        || Volatile.Read(ref _forceFlushWaiters) > 0
        || Volatile.Read(ref _queueCount) >= _maxExportBatchSize;

    private void ExportBatch()
    {
        var requestedCount = Math.Min(
            Volatile.Read(ref _queueCount),
            _maxExportBatchSize);
        if (requestedCount <= 0)
        {
            return;
        }

        var activities = new Activity[requestedCount];
        var count = 0;
        while (count < requestedCount && _queue.TryDequeue(out var activity))
        {
            Interlocked.Decrement(ref _queueCount);
            activities[count++] = activity;
        }

        if (count == 0)
        {
            return;
        }

        var exportResult = ExportResult.Failure;
        try
        {
            using var batch = new Batch<Activity>(activities, count);
            exportResult = exporter.Export(in batch);
        }
        finally
        {
            _health.RecordTraceExport(
                count,
                exportResult is ExportResult.Success);
            Interlocked.Add(ref _processedCount, count);
            lock (_progressSync)
            {
                Monitor.PulseAll(_progressSync);
            }
        }
    }

    private bool WaitForProcessedCount(long target, int timeoutMilliseconds)
    {
        var stopwatch = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();
        lock (_progressSync)
        {
            while (Volatile.Read(ref _processedCount) < target)
            {
                if (!_worker.IsAlive)
                {
                    return false;
                }

                var remaining = GetRemainingMilliseconds(timeoutMilliseconds, stopwatch);
                if (remaining == 0)
                {
                    return false;
                }

                Monitor.Wait(_progressSync, remaining);
            }
        }

        return true;
    }

    private bool JoinIndefinitely()
    {
        _worker.Join();
        return true;
    }

    private static int GetRemainingMilliseconds(
        int timeoutMilliseconds,
        Stopwatch? stopwatch)
    {
        if (timeoutMilliseconds == Timeout.Infinite)
        {
            return Timeout.Infinite;
        }

        var elapsed = stopwatch is null
            ? 0
            : (int)Math.Ceiling(stopwatch.Elapsed.TotalMilliseconds);
        return Math.Max(timeoutMilliseconds - elapsed, 0);
    }

    private void DisposeResources()
    {
        if (Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
        {
            return;
        }

        exporter.Dispose();
        _signal.Dispose();
    }
}
