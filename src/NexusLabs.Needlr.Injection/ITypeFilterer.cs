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
}
