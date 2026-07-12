using OpenTelemetry;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// OpenTelemetry exporter boundary helper with deterministic shutdown coordination.
/// </summary>
internal sealed class ControlledExporter<T> : BaseExporter<T>
    where T : class
{
    private readonly ManualResetEventSlim _shutdownEntered = new(initialState: false);
    private readonly ManualResetEventSlim _shutdownRelease = new(initialState: true);
    private int _disposeCalls;
    private int _shutdownCalls;

    public bool ShutdownSucceeds { get; set; } = true;

    public int DisposeCalls => Volatile.Read(ref _disposeCalls);

    public int ShutdownCalls => Volatile.Read(ref _shutdownCalls);

    public int? LastShutdownTimeoutMilliseconds { get; private set; }

    public override ExportResult Export(in Batch<T> batch) => ExportResult.Success;

    public void BlockShutdown() => _shutdownRelease.Reset();

    public void ReleaseShutdown() => _shutdownRelease.Set();

    public void WaitForShutdown(CancellationToken cancellationToken) =>
        _shutdownEntered.Wait(cancellationToken);

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Interlocked.Increment(ref _shutdownCalls);
        LastShutdownTimeoutMilliseconds = timeoutMilliseconds;
        _shutdownEntered.Set();

        if (_shutdownRelease.IsSet)
        {
            return ShutdownSucceeds;
        }

        var released = timeoutMilliseconds == Timeout.Infinite
            ? WaitIndefinitely()
            : _shutdownRelease.Wait(timeoutMilliseconds);

        return released && ShutdownSucceeds;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref _disposeCalls);
            _shutdownEntered.Dispose();
            _shutdownRelease.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool WaitIndefinitely()
    {
        _shutdownRelease.Wait();
        return true;
    }
}
