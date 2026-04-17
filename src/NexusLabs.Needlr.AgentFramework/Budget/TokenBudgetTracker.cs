namespace NexusLabs.Needlr.AgentFramework.Budget;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-scoped implementation of <see cref="ITokenBudgetTracker"/>
/// with granular input/output/total budget tracking.
/// </summary>
public sealed class TokenBudgetTracker : ITokenBudgetTracker
{
    private static readonly AsyncLocal<ScopeState?> _current = new();

    /// <inheritdoc />
    public IDisposable BeginScope(long maxTokens)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Budget must be greater than zero.");

        return BeginScope(maxInputTokens: null, maxOutputTokens: null, maxTotalTokens: maxTokens);
    }

    /// <inheritdoc />
    public IDisposable BeginScope(long? maxInputTokens = null, long? maxOutputTokens = null, long? maxTotalTokens = null)
    {
        var parent = _current.Value;
        var scope = new ScopeState(maxInputTokens, maxOutputTokens, maxTotalTokens, parent);
        _current.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public IDisposable BeginTrackingScope()
    {
        return BeginScope(null, null, null);
    }

    /// <inheritdoc />
    public IDisposable BeginChildScope(string name, long? maxTokens = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (maxTokens is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Child scope budget must be greater than zero.");

        var parent = _current.Value
            ?? throw new InvalidOperationException("Cannot open a child scope without an active parent scope.");

        var scope = new ScopeState(
            name: name,
            maxInputTokens: null,
            maxOutputTokens: null,
            maxTotalTokens: maxTokens,
            parent: parent);
        _current.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public long CurrentTokens => _current.Value?.CurrentTotalTokens ?? 0L;

    /// <inheritdoc />
    public long CurrentInputTokens => _current.Value?.CurrentInputTokens ?? 0L;

    /// <inheritdoc />
    public long CurrentOutputTokens => _current.Value?.CurrentOutputTokens ?? 0L;

    /// <inheritdoc />
    public long? MaxTokens => _current.Value?.MaxTotalTokens;

    /// <inheritdoc />
    public long? MaxInputTokens => _current.Value?.MaxInputTokens;

    /// <inheritdoc />
    public long? MaxOutputTokens => _current.Value?.MaxOutputTokens;

    /// <inheritdoc />
    public CancellationToken BudgetCancellationToken =>
        _current.Value?.CancellationToken ?? CancellationToken.None;

    /// <inheritdoc />
    public void Record(long tokenCount)
    {
        _current.Value?.AddTotal(tokenCount);
    }

    /// <inheritdoc />
    public void Record(long inputTokens, long outputTokens)
    {
        _current.Value?.AddDetailed(inputTokens, outputTokens);
    }

    private sealed class ScopeState : IDisposable
    {
        private long _currentInputTokens;
        private long _currentOutputTokens;
        private long _currentTotalTokens;
        private readonly CancellationTokenSource _cts;
        private readonly ScopeState? _parent;

        public ScopeState(long? maxInputTokens, long? maxOutputTokens, long? maxTotalTokens, ScopeState? parent = null, string? name = null)
        {
            MaxInputTokens = maxInputTokens;
            MaxOutputTokens = maxOutputTokens;
            MaxTotalTokens = maxTotalTokens;
            _parent = parent;
            Name = name;

            // Link to parent's CTS so parent cancellation cascades to children
            _cts = parent is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(parent.CancellationToken)
                : new CancellationTokenSource();
        }

        public string? Name { get; }
        public long? MaxInputTokens { get; }
        public long? MaxOutputTokens { get; }
        public long? MaxTotalTokens { get; }

        public long CurrentInputTokens => Volatile.Read(ref _currentInputTokens);
        public long CurrentOutputTokens => Volatile.Read(ref _currentOutputTokens);
        public long CurrentTotalTokens => Volatile.Read(ref _currentTotalTokens);

        public CancellationToken CancellationToken => _cts.Token;

        public void AddTotal(long tokens)
        {
            var newTotal = Interlocked.Add(ref _currentTotalTokens, tokens);
            CheckBudget(CurrentInputTokens, CurrentOutputTokens, newTotal);
            _parent?.AddTotal(tokens);
        }

        public void AddDetailed(long inputTokens, long outputTokens)
        {
            var newInput = Interlocked.Add(ref _currentInputTokens, inputTokens);
            var newOutput = Interlocked.Add(ref _currentOutputTokens, outputTokens);
            var newTotal = Interlocked.Add(ref _currentTotalTokens, inputTokens + outputTokens);
            CheckBudget(newInput, newOutput, newTotal);
            _parent?.AddDetailed(inputTokens, outputTokens);
        }

        private void CheckBudget(long currentInput, long currentOutput, long currentTotal)
        {
            if (_cts.IsCancellationRequested)
                return;

            bool exceeded =
                (MaxTotalTokens.HasValue && currentTotal >= MaxTotalTokens.Value) ||
                (MaxInputTokens.HasValue && currentInput >= MaxInputTokens.Value) ||
                (MaxOutputTokens.HasValue && currentOutput >= MaxOutputTokens.Value);

            if (exceeded)
            {
                _cts.Cancel();
            }
        }

        public void Dispose()
        {
            _current.Value = _parent;
            _cts.Dispose();
        }
    }
}
