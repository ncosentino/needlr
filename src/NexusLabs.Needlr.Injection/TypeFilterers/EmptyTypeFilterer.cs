namespace NexusLabs.Needlr.Injection.TypeFilterers;

public sealed class EmptyTypeFilterer : ITypeFilterer
{
    private static readonly Lazy<EmptyTypeFilterer> _instance = new(() => new EmptyTypeFilterer());

    public static EmptyTypeFilterer Instance => _instance.Value;

    public bool IsInjectableScopedType(Type type)
        => false;
    public bool IsInjectableTransientType(Type type)
        => false;
    public bool IsInjectableSingletonType(Type type)
        => false;
}
