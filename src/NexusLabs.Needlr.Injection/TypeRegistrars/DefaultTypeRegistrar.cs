using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.TypeRegistrars;

public sealed class DefaultTypeRegistrar : ITypeRegistrar
{
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

        // Register as interfaces (excluding system interfaces and generic type definitions)
        var interfaces = type.GetInterfaces()
            .Where(i => !i.IsGenericTypeDefinition)
            .Where(i => i.Assembly != typeof(object).Assembly) // Skip system interfaces
            .Where(i => !i.Name.StartsWith("System.")) // Additional system interface filtering
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
}
