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
}
