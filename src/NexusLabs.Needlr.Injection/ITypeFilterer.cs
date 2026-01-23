namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Defines a filterer that determines the service lifetime for discovered types.
/// Implement this interface to customize how types are categorized as scoped, transient, or singleton.
/// </summary>
[DoNotAutoRegister]
public interface ITypeFilterer
{
    bool IsInjectableScopedType(
        Type type);

    bool IsInjectableTransientType(
        Type type);

    bool IsInjectableSingletonType(
        Type type);

    /// <summary>
    /// Determines whether a type should be excluded from registration.
    /// Used by source-gen registrars where lifetimes are pre-computed.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type should be excluded; otherwise false.</returns>
    bool IsTypeExcluded(Type type) => false;

    /// <summary>
    /// Gets the effective lifetime for a type, considering any overrides applied via
    /// UsingOnlyAsTransient, UsingOnlyAsSingleton, or UsingOnlyAsScoped decorators.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="defaultLifetime">The default lifetime if no override applies.</param>
    /// <returns>The effective lifetime for the type.</returns>
    TypeFiltererLifetime GetEffectiveLifetime(Type type, TypeFiltererLifetime defaultLifetime)
    {
        // Check for explicit lifetime overrides in priority order
        if (IsInjectableTransientType(type))
            return TypeFiltererLifetime.Transient;
        if (IsInjectableScopedType(type))
            return TypeFiltererLifetime.Scoped;
        if (IsInjectableSingletonType(type))
            return TypeFiltererLifetime.Singleton;

        return defaultLifetime;
    }
}

/// <summary>
/// Represents a service lifetime for type filtering purposes.
/// </summary>
public enum TypeFiltererLifetime
{
    Singleton,
    Scoped,
    Transient
}
