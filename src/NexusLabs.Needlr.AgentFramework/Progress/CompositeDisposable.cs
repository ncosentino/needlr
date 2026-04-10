namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Composite <see cref="IDisposable"/> that owns a fixed set of child disposables
/// and releases them in reverse order on <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// <para>
/// Null entries are ignored, allowing callers to mix typed references with
/// conditional <c>as IDisposable</c> casts without pre-filtering. All entries
/// are disposed even if earlier ones throw; collected exceptions are re-thrown
/// as a single <see cref="AggregateException"/>.
/// </para>
/// <para>
/// Used primarily by the source generator's <c>BeginXxxAgentProgressScope</c>
/// emission to tie the lifetime of per-scope sinks (that implement
/// <see cref="IDisposable"/>) to the returned scope handle, preventing leaks.
/// </para>
/// </remarks>
public sealed class CompositeDisposable : IDisposable
{
    private readonly IDisposable?[] _disposables;
    private bool _disposed;

    /// <summary>
    /// Creates a composite wrapping the supplied disposables in order.
    /// </summary>
    public CompositeDisposable(IEnumerable<IDisposable?> disposables)
    {
        ArgumentNullException.ThrowIfNull(disposables);
        _disposables = disposables.ToArray();
    }

    /// <summary>
    /// Creates a composite wrapping the supplied disposables in order.
    /// </summary>
    public CompositeDisposable(params IDisposable?[] disposables)
    {
        ArgumentNullException.ThrowIfNull(disposables);
        _disposables = disposables;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        List<Exception>? errors = null;
        for (int i = _disposables.Length - 1; i >= 0; i--)
        {
            try
            {
                _disposables[i]?.Dispose();
            }
            catch (Exception ex)
            {
                errors ??= new List<Exception>();
                errors.Add(ex);
            }
        }

        if (errors is not null)
            throw new AggregateException(errors);
    }
}
