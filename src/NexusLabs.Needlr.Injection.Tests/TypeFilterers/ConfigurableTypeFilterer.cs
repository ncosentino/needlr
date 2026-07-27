using NexusLabs.Needlr;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

/// <summary>
/// An <see cref="ITypeFilterer"/> whose answers are fixed per lifetime so tests can drive
/// every combination of underlying filter results through the public extension methods.
/// </summary>
[DoNotAutoRegister]
internal sealed class ConfigurableTypeFilterer : ITypeFilterer
{
    private readonly bool _isScoped;
    private readonly bool _isTransient;
    private readonly bool _isSingleton;
    private readonly Predicate<Type> _exclusionPredicate;

    public ConfigurableTypeFilterer(
        bool isScoped,
        bool isTransient,
        bool isSingleton)
        : this(isScoped, isTransient, isSingleton, static _ => false)
    {
    }

    public ConfigurableTypeFilterer(
        bool isScoped,
        bool isTransient,
        bool isSingleton,
        Predicate<Type> exclusionPredicate)
    {
        _isScoped = isScoped;
        _isTransient = isTransient;
        _isSingleton = isSingleton;
        _exclusionPredicate = exclusionPredicate;
    }

    public bool IsInjectableScopedType(Type type) => _isScoped;

    public bool IsInjectableTransientType(Type type) => _isTransient;

    public bool IsInjectableSingletonType(Type type) => _isSingleton;

    public bool IsTypeExcluded(Type type) => _exclusionPredicate.Invoke(type);
}
