namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IProgressReporterAccessor"/>.
/// </summary>
internal sealed class ProgressReporterAccessor : IProgressReporterAccessor
{
    private static readonly AsyncLocal<IProgressReporter?> _current = new();

    /// <inheritdoc />
    public IProgressReporter Current => _current.Value ?? NullProgressReporter.Instance;

    /// <inheritdoc />
    public IDisposable BeginScope(IProgressReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);

        var previous = _current.Value;
        _current.Value = reporter;
        return new Scope(previous);
    }

    private sealed class Scope(IProgressReporter? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _current.Value = previous;
        }
    }
}
