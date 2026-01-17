using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.TypeRegistrars;

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
        var types = GetInjectableTypes(assemblies);

        foreach (var typeInfo in types)
        {
            var type = typeInfo.Type;

            if (typeFilterer.IsInjectableSingletonType(type))
            {
                RegisterTypeAsSelfWithInterfaces(services, type, typeInfo.Interfaces, ServiceLifetime.Singleton);
            }
            else if (typeFilterer.IsInjectableTransientType(type))
            {
                RegisterTypeAsSelfWithInterfaces(services, type, typeInfo.Interfaces, ServiceLifetime.Transient);
            }
            else if (typeFilterer.IsInjectableScopedType(type))
            {
                RegisterTypeAsSelfWithInterfaces(services, type, typeInfo.Interfaces, ServiceLifetime.Scoped);
            }
        }
    }

    private IReadOnlyList<InjectableTypeInfo> GetInjectableTypes(IReadOnlyList<Assembly> assemblies)
    {
        if (_typeProvider != null)
        {
            return _typeProvider();
        }

        // Try to find the generated TypeRegistry in the assemblies
        foreach (var assembly in assemblies)
        {
            var registryType = assembly.GetType("NexusLabs.Needlr.Generated.TypeRegistry");
            if (registryType != null)
            {
                var method = registryType.GetMethod(
                    "GetInjectableTypes",
                    BindingFlags.Public | BindingFlags.Static);

                if (method != null)
                {
                    var result = method.Invoke(null, null);
                    if (result is IReadOnlyList<InjectableTypeInfo> types)
                    {
                        return types;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "Could not locate the generated TypeRegistry. " +
            "Ensure you have added the [assembly: GenerateTypeRegistry(...)] attribute " +
            "and the NexusLabs.Needlr.Generators package to your project. " +
            "The generated TypeRegistry class should be at 'NexusLabs.Needlr.Generated.TypeRegistry'.");
    }

    private static void RegisterTypeAsSelfWithInterfaces(
        IServiceCollection services,
        Type type,
        IReadOnlyList<Type> interfaces,
        ServiceLifetime lifetime)
    {
        // Register as self
        services.Add(new ServiceDescriptor(type, type, lifetime));

        // Register as interfaces
        foreach (var interfaceType in interfaces)
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
                // For transient/scoped services, direct registration is fine
                services.Add(new ServiceDescriptor(interfaceType, type, lifetime));
            }
        }
    }
}
