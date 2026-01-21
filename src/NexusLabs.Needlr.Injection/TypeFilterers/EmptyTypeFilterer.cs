namespace NexusLabs.Needlr.Injection.TypeFilterers;

/// <summary>
/// A type filterer that rejects all types. Use as a base when building custom filters.
/// </summary>
public sealed class EmptyTypeFilterer : ITypeFilterer
{
    private static readonly Lazy<EmptyTypeFilterer> _instance = new(() => new EmptyTypeFilterer());

    /// <summary>
    /// Gets the singleton instance of the empty type filterer.
    /// </summary>
    public static EmptyTypeFilterer Instance => _instance.Value;

    /// <inheritdoc />
    public bool IsInjectableScopedType(Type type)
        => false;

    /// <inheritdoc />
    public bool IsInjectableTransientType(Type type)
        => false;

    /// <inheritdoc />
    public bool IsInjectableSingletonType(Type type)
        => false;
}
