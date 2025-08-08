namespace NexusLabs.Needlr.Injection.TypeFilterers;

public sealed class TypeFilterDecorator : ITypeFilterer
{
    private readonly ITypeFilterer _innerFilterer;
    private readonly Predicate<Type> _filter;
    private readonly Predicate<Type> _scopedTypeFilter;
    private readonly Predicate<Type> _singletonTypeFilter;

    public TypeFilterDecorator(
        ITypeFilterer innerFilterer,
        Predicate<Type> typeFilter,
        Predicate<Type> scopedTypeFilter,
        Predicate<Type> singletonTypeFilter)
    {
        ArgumentNullException.ThrowIfNull(innerFilterer);
        ArgumentNullException.ThrowIfNull(typeFilter);
        ArgumentNullException.ThrowIfNull(scopedTypeFilter);
        ArgumentNullException.ThrowIfNull(singletonTypeFilter);

        _innerFilterer = innerFilterer;
        _filter = typeFilter;
        _scopedTypeFilter = scopedTypeFilter;
        _singletonTypeFilter = singletonTypeFilter;
    }

    public bool IsInjectableType(Type type)
        => _innerFilterer.IsInjectableType(type) && _filter.Invoke(type);
    
    public bool IsInjectableTransientType(Type type)
        => _innerFilterer.IsInjectableTransientType(type) && _scopedTypeFilter.Invoke(type);
    
    public bool IsInjectableSingletonType(Type type)
        => _innerFilterer.IsInjectableSingletonType(type) && _singletonTypeFilter.Invoke(type);
}
