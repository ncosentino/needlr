using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;

/// <summary>
/// A type filterer that uses compile-time generated lifetime information
/// instead of runtime reflection for constructor analysis.
/// </summary>
/// <remarks>
/// <para>
/// When using the generated type registrar with pre-computed lifetimes,
/// this filterer is effectively a no-op since all lifetime decisions are
/// made at compile time. It exists for API compatibility and as a safety
/// fallback that returns false for all checks.
/// </para>
/// <para>
/// For zero-reflection scenarios, use this filterer together with
/// <see cref="TypeRegistrars.GeneratedTypeRegistrar"/> and explicitly
/// provide the type provider function.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Zero-reflection configuration:
/// var syringe = new Syringe()
///     .UsingGeneratedTypeRegistrar(TypeRegistry.GetInjectableTypes)
///     .UsingGeneratedTypeFilterer()
///     .UsingGeneratedPluginFactory(TypeRegistry.GetPluginTypes);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class GeneratedTypeFilterer : ITypeFilterer
{
    private readonly IReadOnlyDictionary<Type, InjectableLifetime>? _lifetimeLookup;

    /// <summary>
    /// Initializes a new instance that returns false for all type checks.
    /// </summary>
    /// <remarks>
    /// Use this when all lifetime information is pre-computed by the source generator.
    /// The <see cref="TypeRegistrars.GeneratedTypeRegistrar"/> will use pre-computed
    /// lifetimes directly and will not need to call this filterer.
    /// </remarks>
    public GeneratedTypeFilterer()
    {
        _lifetimeLookup = null;
    }

    /// <summary>
    /// Initializes a new instance with pre-computed lifetime information.
    /// </summary>
    /// <param name="typeProvider">A function that returns the injectable types with pre-computed lifetimes.</param>
    /// <remarks>
    /// This constructor builds a lookup table from the provided type information,
    /// enabling reflection-free lifetime checks.
    /// </remarks>
    public GeneratedTypeFilterer(Func<IReadOnlyList<InjectableTypeInfo>> typeProvider)
    {
        ArgumentNullException.ThrowIfNull(typeProvider);

        var types = typeProvider();
        var lookup = new Dictionary<Type, InjectableLifetime>(types.Count);

        foreach (var info in types)
        {
            if (info.Lifetime.HasValue)
            {
                lookup[info.Type] = info.Lifetime.Value;
            }
        }

        _lifetimeLookup = lookup;
    }

    /// <inheritdoc />
    public bool IsInjectableSingletonType(Type type)
    {
        if (_lifetimeLookup is null)
            return false;

        return _lifetimeLookup.TryGetValue(type, out var lifetime) &&
               lifetime == InjectableLifetime.Singleton;
    }

    /// <inheritdoc />
    public bool IsInjectableTransientType(Type type)
    {
        if (_lifetimeLookup is null)
            return false;

        return _lifetimeLookup.TryGetValue(type, out var lifetime) &&
               lifetime == InjectableLifetime.Transient;
    }

    /// <inheritdoc />
    public bool IsInjectableScopedType(Type type)
    {
        if (_lifetimeLookup is null)
            return false;

        return _lifetimeLookup.TryGetValue(type, out var lifetime) &&
               lifetime == InjectableLifetime.Scoped;
    }
}
