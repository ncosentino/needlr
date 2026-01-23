using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.SourceGen.TypeRegistrars;

/// <summary>
/// A type registrar that uses compile-time generated type registry
/// instead of runtime reflection for type discovery.
/// </summary>
/// <remarks>
/// <para>
/// This registrar is designed to work with the source generator from
/// <c>NexusLabs.Needlr.Generators</c>. To use it:
/// </para>
/// <list type="number">
/// <item>Add the <c>NexusLabs.Needlr.Generators</c> package to your project</item>
/// <item>Add <c>[assembly: GenerateTypeRegistry(...)]</c> attribute</item>
/// <item>Use <c>.UsingGeneratedTypeRegistrar()</c> when building the service provider</item>
/// </list>
/// <para>
/// The assemblies parameter is ignored when a type provider is supplied,
/// as all type discovery happens at compile time.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In your assembly (typically Program.cs or a dedicated file):
/// [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyCompany", "NexusLabs" })]
///
/// // When building the service provider:
/// var serviceProvider = new Syringe()
///     .UsingGeneratedTypeRegistrar()
///     .BuildServiceProvider(config);
/// </code>
/// </example>
public sealed class GeneratedTypeRegistrar : ITypeRegistrar
{
    private readonly Func<IReadOnlyList<InjectableTypeInfo>>? _typeProvider;

    /// <summary>
    /// Initializes a new instance using the default generated TypeRegistry.
    /// </summary>
    /// <remarks>
    /// This constructor uses reflection to locate the generated TypeRegistry class.
    /// The generated class must be present in the calling assembly.
    /// </remarks>
    public GeneratedTypeRegistrar()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom type provider.
    /// </summary>
    /// <param name="typeProvider">
    /// A function that returns the injectable types.
    /// If null, the registrar will attempt to locate the generated TypeRegistry at runtime.
    /// </param>
    public GeneratedTypeRegistrar(Func<IReadOnlyList<InjectableTypeInfo>>? typeProvider)
    {
        _typeProvider = typeProvider;
    }

    /// <inheritdoc />
    public void RegisterTypesFromAssemblies(
        IServiceCollection services,
        ITypeFilterer typeFilterer,
        IReadOnlyList<Assembly> assemblies)
    {
        var types = _typeProvider?.Invoke() ?? [];

        foreach (var typeInfo in types)
        {
            // Use pre-computed lifetime - no reflection needed
            // The source generator only emits types with valid lifetimes
            if (!typeInfo.Lifetime.HasValue)
            {
                continue;
            }

            var type = typeInfo.Type;

            // Check if type is excluded via Except<T>() or Except(predicate)
            if (typeFilterer.IsTypeExcluded(type))
            {
                continue;
            }

            // Get effective lifetime, allowing type filterer to override pre-computed lifetime
            // This enables UsingOnlyAsTransient<T>(), UsingOnlyAsSingleton<T>(), etc.
            var defaultLifetime = ConvertToFiltererLifetime(typeInfo.Lifetime.Value);
            var effectiveLifetime = typeFilterer.GetEffectiveLifetime(type, defaultLifetime);
            var serviceLifetime = ConvertToServiceLifetime(effectiveLifetime);
            
            RegisterTypeAsSelfWithInterfaces(services, typeInfo, serviceLifetime);
        }
    }

    private static TypeFiltererLifetime ConvertToFiltererLifetime(InjectableLifetime lifetime)
    {
        return lifetime switch
        {
            InjectableLifetime.Singleton => TypeFiltererLifetime.Singleton,
            InjectableLifetime.Scoped => TypeFiltererLifetime.Scoped,
            InjectableLifetime.Transient => TypeFiltererLifetime.Transient,
            _ => TypeFiltererLifetime.Singleton
        };
    }

    private static ServiceLifetime ConvertToServiceLifetime(TypeFiltererLifetime lifetime)
    {
        return lifetime switch
        {
            TypeFiltererLifetime.Singleton => ServiceLifetime.Singleton,
            TypeFiltererLifetime.Scoped => ServiceLifetime.Scoped,
            TypeFiltererLifetime.Transient => ServiceLifetime.Transient,
            _ => ServiceLifetime.Singleton
        };
    }

    private static void RegisterTypeAsSelfWithInterfaces(
        IServiceCollection services,
        InjectableTypeInfo typeInfo,
        ServiceLifetime lifetime)
    {
        var type = typeInfo.Type;
        var factory = typeInfo.Factory;

        // Factory is always provided by source generator - required for AOT compatibility
        if (factory is null)
        {
            throw new InvalidOperationException(
                $"No factory delegate provided for type '{type.FullName}'. " +
                "This indicates the type was not processed by the Needlr source generator. " +
                "Ensure the type is included in the GenerateTypeRegistry namespace prefixes.");
        }

        // Register as self using factory (AOT-compatible, no reflection needed)
        services.Add(new ServiceDescriptor(type, factory, lifetime));

        // Register as interfaces
        foreach (var interfaceType in typeInfo.Interfaces)
        {
            // For singletons, register interfaces as factory delegates to ensure same instance
            if (lifetime == ServiceLifetime.Singleton)
            {
                services.Add(new ServiceDescriptor(
                    interfaceType,
                    serviceProvider => serviceProvider.GetRequiredService(type),
                    lifetime));
            }
            else
            {
                // For transient/scoped services, use the factory
                services.Add(new ServiceDescriptor(interfaceType, factory, lifetime));
            }
        }
    }
}
