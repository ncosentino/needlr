namespace NexusLabs.Needlr.AgentFramework.Collectors;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IAgentOutputCollectorAccessor{T}"/>.
/// Uses the mutable-holder pattern so items added by tools in child async flows are visible
/// to the parent scope.
/// </summary>
internal sealed class AgentOutputCollectorAccessor<T> : IAgentOutputCollectorAccessor<T>
{
    private static readonly AsyncLocal<IAgentOutputCollector<T>?> CurrentCollector = new();

    /// <inheritdoc />
    public IAgentOutputCollector<T>? Current => CurrentCollector.Value;

    /// <inheritdoc />
    public IDisposable BeginScope() =>
        BeginScope(new AgentOutputCollector<T>());

    /// <inheritdoc />
    public IDisposable BeginScope(IAgentOutputCollector<T> collector)
    {
        ArgumentNullException.ThrowIfNull(collector);

        var previous = CurrentCollector.Value;
        CurrentCollector.Value = collector;
        return new Scope(previous);
    }

    private sealed class Scope(IAgentOutputCollector<T>? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentCollector.Value = previous;
        }
    }
}
