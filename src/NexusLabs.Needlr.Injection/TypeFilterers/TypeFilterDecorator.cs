namespace NexusLabs.Needlr.Injection.TypeFilterers;

/// <summary>
/// A decorator that wraps an <see cref="ITypeFilterer"/> and modifies its filtering behavior.
/// Allows customization of lifetime categorization without replacing the entire filterer.
/// </summary>
public sealed class TypeFilterDecorator : ITypeFilterer
{
    private readonly ITypeFilterer _innerFilterer;
    private readonly Func<Predicate<Type>, Type, bool> _scopedTypeFilterer;
    private readonly Func<Predicate<Type>, Type, bool> _transientTypeFilter;
    private readonly Func<Predicate<Type>, Type, bool> _singletonTypeFilter;

    public TypeFilterDecorator(
        ITypeFilterer innerFilterer,
        Func<Predicate<Type>, Type, bool> scopedTypeFilterer,
        Func<Predicate<Type>, Type, bool> transientTypeFilterer,
        Func<Predicate<Type>, Type, bool> singletonTypeFilter)
    {
        ArgumentNullException.ThrowIfNull(innerFilterer);
        ArgumentNullException.ThrowIfNull(scopedTypeFilterer);
        ArgumentNullException.ThrowIfNull(transientTypeFilterer);
        ArgumentNullException.ThrowIfNull(singletonTypeFilter);

        _innerFilterer = innerFilterer;
        _scopedTypeFilterer = scopedTypeFilterer;
        _transientTypeFilter = transientTypeFilterer;
        _singletonTypeFilter = singletonTypeFilter;
    }

    /// <inheritdoc />
    public bool IsInjectableScopedType(Type type)
        => _scopedTypeFilterer.Invoke(
            _innerFilterer.IsInjectableScopedType, 
            type);

    /// <inheritdoc />
    public bool IsInjectableTransientType(Type type)
        => _transientTypeFilter.Invoke(
            _innerFilterer.IsInjectableTransientType, 
            type);

    /// <inheritdoc />
    public bool IsInjectableSingletonType(Type type)
        => _singletonTypeFilter.Invoke(
            _innerFilterer.IsInjectableSingletonType, 
            type);
}
