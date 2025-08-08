namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public interface ITypeFilterer
{
    bool IsInjectableType(
        Type type);

    bool IsInjectableTransientType(
        Type type);

    bool IsInjectableSingletonType(
        Type type);
}
