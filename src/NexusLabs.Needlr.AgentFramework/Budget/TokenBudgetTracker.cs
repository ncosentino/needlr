namespace NexusLabs.Needlr.AgentFramework.Budget;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-scoped implementation of <see cref="ITokenBudgetTracker"/>.
/// </summary>
/// <remarks>
/// Each call to <see cref="BeginScope"/> opens an independent budget window in the calling
/// async context. Concurrent pipeline runs each see their own token count — the same
/// isolation model as <c>IHttpContextAccessor</c>.
/// </remarks>
public sealed class TokenBudgetTracker : ITokenBudgetTracker
{
    private static readonly AsyncLocal<ScopeState?> _current = new();

    /// <inheritdoc />
    public IDisposable BeginScope(long maxTokens)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Budget must be greater than zero.");

        var scope = new ScopeState(maxTokens);
        _current.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public long CurrentTokens => _current.Value?.CurrentTokens ?? 0L;

    /// <inheritdoc />
    public long? MaxTokens => _current.Value?.MaxTokens;

    /// <inheritdoc />
    public CancellationToken BudgetCancellationToken =>
        _current.Value?.CancellationToken ?? CancellationToken.None;

    /// <inheritdoc />
    public void Record(long tokenCount)
    {
        _current.Value?.Add(tokenCount);
    }

    private sealed class ScopeState : IDisposable
    {
        private long _currentTokens;
        private readonly CancellationTokenSource _cts = new();

        public ScopeState(long maxTokens) => MaxTokens = maxTokens;

        public long MaxTokens { get; }

        public long CurrentTokens => Volatile.Read(ref _currentTokens);

        public CancellationToken CancellationToken => _cts.Token;

        public void Add(long tokens)
        {
            var newTotal = Interlocked.Add(ref _currentTokens, tokens);

            // Cancel the token when budget exceeded — this stops MAF workflows
            if (newTotal >= MaxTokens && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        public void Dispose()
        {
            _current.Value = null;
            _cts.Dispose();
        }
    }
}
