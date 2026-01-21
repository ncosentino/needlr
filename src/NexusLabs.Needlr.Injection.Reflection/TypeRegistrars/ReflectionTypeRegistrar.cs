using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection.TypeRegistrars;

/// <summary>
/// Type registrar that uses runtime reflection to discover and register types.
/// </summary>
/// <remarks>
/// This registrar is not compatible with NativeAOT or trimming. For AOT scenarios,
/// use GeneratedTypeRegistrar from NexusLabs.Needlr.Injection.SourceGen with the Needlr source generator instead.
/// </remarks>
[RequiresUnreferencedCode("ReflectionTypeRegistrar uses reflection to discover types. Use GeneratedTypeRegistrar for AOT scenarios.")]
public sealed class ReflectionTypeRegistrar : ITypeRegistrar
{
    /// <inheritdoc />
    public void RegisterTypesFromAssemblies(
        IServiceCollection services,
        ITypeFilterer typeFilterer,
        IReadOnlyList<Assembly> assemblies)
    {
        var allTypes = assemblies
            .SelectMany(assembly => GetAllTypes(assembly))
            .Where(type => type is not null)
            .Where(type => !HasDoNotAutoRegisterAttribute(type))
            .ToList();

        foreach (var type in allTypes)
        {
            if (typeFilterer.IsInjectableSingletonType(type))
            {
                RegisterTypeAsSelfWithInterfaces(services, type, ServiceLifetime.Singleton);
            }
            else if (typeFilterer.IsInjectableTransientType(type))
            {
                RegisterTypeAsSelfWithInterfaces(services, type, ServiceLifetime.Transient);
            }
            else if (typeFilterer.IsInjectableScopedType(type))
            {
                RegisterTypeAsSelfWithInterfaces(services, type, ServiceLifetime.Scoped);
            }
        }
    }

    private static Type[] GetAllTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return only the types that loaded successfully
            return ex.Types.Where(t => t is not null).ToArray()!;
        }
        catch
        {
            // If we can't load types from the assembly, skip it
            return [];
        }
    }

    private static bool HasDoNotAutoRegisterAttribute(Type type)
    {
        return type.GetCustomAttribute<DoNotAutoRegisterAttribute>(inherit: true) is not null ||
            type.GetInterfaces().Any(t => HasDoNotAutoRegisterAttribute(t));
    }

    private static void RegisterTypeAsSelfWithInterfaces(
        IServiceCollection services, 
        Type type, ServiceLifetime lifetime)
    {
        // Register as self
        services.Add(new ServiceDescriptor(type, type, lifetime));

        // Get constructor parameter types to detect decorator pattern
        var constructorParamTypes = GetConstructorParameterTypes(type);

        // Register as interfaces (excluding system interfaces, generic type definitions, and decorator interfaces)
        var interfaces = type.GetInterfaces()
            .Where(i => !i.IsGenericTypeDefinition)
            .Where(i => i.Assembly != typeof(object).Assembly) // Skip system interfaces
            .Where(i => !i.Name.StartsWith("System.")) // Additional system interface filtering
            .Where(i => !IsDecoratorInterface(i, constructorParamTypes)) // Skip decorator pattern interfaces
            .ToList();

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
                // For transient services, direct registration is fine
                services.Add(new ServiceDescriptor(interfaceType, type, lifetime));
            }
        }
    }

    /// <summary>
    /// Gets all parameter types from all constructors of a type.
    /// </summary>
    private static HashSet<Type> GetConstructorParameterTypes(Type type)
    {
        var paramTypes = new HashSet<Type>();

        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var param in ctor.GetParameters())
            {
                paramTypes.Add(param.ParameterType);
            }
        }

        return paramTypes;
    }

    /// <summary>
    /// Checks if an interface is a decorator interface (type implements it and also takes it as a constructor parameter).
    /// A type that implements IFoo and takes IFoo in its constructor is likely a decorator
    /// and should not be auto-registered as IFoo to avoid circular dependencies.
    /// </summary>
    private static bool IsDecoratorInterface(Type interfaceType, HashSet<Type> constructorParamTypes)
    {
        return constructorParamTypes.Contains(interfaceType);
    }
}
