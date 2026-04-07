namespace NexusLabs.Needlr.AgentFramework.Context;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IAgentExecutionContextAccessor"/>.
/// Each async flow sees its own context — concurrent orchestrations are naturally isolated.
/// </summary>
internal sealed class AgentExecutionContextAccessor : IAgentExecutionContextAccessor
{
    private static readonly AsyncLocal<IAgentExecutionContext?> CurrentContext = new();

    /// <inheritdoc />
    public IAgentExecutionContext? Current => CurrentContext.Value;

    /// <inheritdoc />
    public IDisposable BeginScope(IAgentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = CurrentContext.Value;
        CurrentContext.Value = context;
        return new Scope(previous);
    }

    private sealed class Scope(IAgentExecutionContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CurrentContext.Value = previous;
        }
    }
}
