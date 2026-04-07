namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IAgentDiagnosticsAccessor"/>
/// using the mutable-holder pattern. A mutable <see cref="Holder"/> reference is stored in the
/// <see cref="AsyncLocal{T}"/> slot — because the reference flows downward, mutations made by
/// child async flows (middleware) are visible to the parent scope.
/// </summary>
internal sealed class AgentDiagnosticsAccessor : IAgentDiagnosticsAccessor
{
    private static readonly AsyncLocal<Holder?> Current = new();

    /// <inheritdoc />
    public IAgentRunDiagnostics? LastRunDiagnostics => Current.Value?.Value;

    /// <inheritdoc />
    public IDisposable BeginCapture()
    {
        var previous = Current.Value;
        Current.Value = new Holder();
        return new Scope(previous);
    }

    /// <summary>
    /// Stores completed diagnostics into the current holder. Called by the diagnostics
    /// middleware after an agent run completes.
    /// </summary>
    internal void Set(IAgentRunDiagnostics diagnostics)
    {
        if (Current.Value is { } holder)
        {
            holder.Value = diagnostics;
        }
        else
        {
            Current.Value = new Holder { Value = diagnostics };
        }
    }

    private sealed class Holder
    {
        public IAgentRunDiagnostics? Value;
    }

    private sealed class Scope(Holder? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Current.Value = previous;
        }
    }
}
