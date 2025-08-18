namespace NexusLabs.Needlr.Injection;

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
