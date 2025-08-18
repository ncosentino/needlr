namespace NexusLabs.Needlr.Injection.TypeFilterers;

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

    public bool IsInjectableScopedType(Type type)
        => _scopedTypeFilterer.Invoke(
            _innerFilterer.IsInjectableScopedType, 
            type);
    
    public bool IsInjectableTransientType(Type type)
        => _transientTypeFilter.Invoke(
            _innerFilterer.IsInjectableTransientType, 
            type);
    
    public bool IsInjectableSingletonType(Type type)
        => _singletonTypeFilter.Invoke(
            _innerFilterer.IsInjectableSingletonType, 
            type);
}
