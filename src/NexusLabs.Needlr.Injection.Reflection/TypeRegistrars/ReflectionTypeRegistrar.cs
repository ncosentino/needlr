using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            .Where(type => type.IsClass && !type.IsAbstract) // Only concrete classes
            .Where(type => !HasDoNotAutoRegisterAttribute(type))
            .ToList();

        foreach (var type in allTypes)
        {
            // Check if type is excluded via Except<T>() or Except(predicate)
            if (typeFilterer.IsTypeExcluded(type))
            {
                continue;
            }

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

        // Register hosted services discovered via BackgroundService/IHostedService
        // Must happen BEFORE decorators so that decorators can wrap the hosted services
        RegisterHostedServices(services, allTypes);

        // Apply decorators discovered via [DecoratorFor<T>] attributes
        ApplyDecoratorForAttributes(services, allTypes);
    }

    /// <summary>
    /// Discovers and registers types that are hosted services (inherit from BackgroundService
    /// or implement IHostedService directly).
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services, List<Type> allTypes)
    {
        var hostedServiceTypes = allTypes
            .Where(IsHostedServiceType)
            .ToList();

        foreach (var type in hostedServiceTypes)
        {
            // Skip if already registered as concrete type (from regular registration)
            var existingRegistration = services.FirstOrDefault(d => d.ServiceType == type);
            if (existingRegistration is null)
            {
                // Register concrete type as singleton
                services.AddSingleton(type);
            }

            // Register as IHostedService forwarding to concrete type
            services.AddSingleton<IHostedService>(sp => (IHostedService)sp.GetRequiredService(type));
        }
    }

    /// <summary>
    /// Determines if a type is a hosted service (inherits from BackgroundService
    /// or implements IHostedService directly, excluding abstract classes and decorators).
    /// </summary>
    private static bool IsHostedServiceType(Type type)
    {
        if (!type.IsClass || type.IsAbstract)
            return false;

        // Skip types that are decorators for IHostedService
        if (IsDecoratorForHostedService(type))
            return false;

        // Check if inherits from BackgroundService
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            if (baseType.FullName == "Microsoft.Extensions.Hosting.BackgroundService")
                return true;
            baseType = baseType.BaseType;
        }

        // Check if directly implements IHostedService
        return type.GetInterfaces().Any(i => i.FullName == "Microsoft.Extensions.Hosting.IHostedService");
    }

    /// <summary>
    /// Checks if a type has [DecoratorFor&lt;IHostedService&gt;] attribute.
    /// </summary>
    private static bool IsDecoratorForHostedService(Type type)
    {
        foreach (var attribute in type.GetCustomAttributes(inherit: false))
        {
            var attrType = attribute.GetType();
            if (!attrType.IsGenericType)
                continue;

            var genericTypeDef = attrType.GetGenericTypeDefinition();
            if (genericTypeDef.FullName?.StartsWith("NexusLabs.Needlr.DecoratorForAttribute`1", StringComparison.Ordinal) != true)
                continue;

            // Get the type argument
            var typeArgs = attrType.GetGenericArguments();
            if (typeArgs.Length == 1 && typeArgs[0].FullName == "Microsoft.Extensions.Hosting.IHostedService")
                return true;
        }
        return false;
    }

    /// <summary>
    /// Discovers and applies decorators marked with [DecoratorFor&lt;T&gt;] attributes.
    /// </summary>
    private static void ApplyDecoratorForAttributes(IServiceCollection services, List<Type> allTypes)
    {
        var decoratorInfos = new List<(Type DecoratorType, Type ServiceType, int Order)>();

        foreach (var type in allTypes)
        {
            foreach (var attribute in type.GetCustomAttributes(inherit: false))
            {
                var attrType = attribute.GetType();
                if (!attrType.IsGenericType)
                    continue;

                var genericTypeDef = attrType.GetGenericTypeDefinition();
                if (genericTypeDef.FullName?.StartsWith("NexusLabs.Needlr.DecoratorForAttribute`1", StringComparison.Ordinal) != true)
                    continue;

                // Get the service type from the generic type argument
                var serviceType = attrType.GetGenericArguments()[0];

                // Get the Order property value
                var orderProperty = attrType.GetProperty("Order");
                var order = orderProperty?.GetValue(attribute) as int? ?? 0;

                decoratorInfos.Add((type, serviceType, order));
            }
        }

        // Group by service type and apply in order
        var decoratorsByService = decoratorInfos
            .GroupBy(d => d.ServiceType)
            .OrderBy(g => g.Key.FullName);

        foreach (var serviceGroup in decoratorsByService)
        {
            foreach (var decorator in serviceGroup.OrderBy(d => d.Order))
            {
                try
                {
                    services.AddDecorator(decorator.ServiceType, decorator.DecoratorType);
                }
                catch (InvalidOperationException)
                {
                    // Service not registered - skip this decorator
                    // This can happen if the base service wasn't registered (e.g., excluded)
                }
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

        // Check for [RegisterAs<T>] attributes - if present, only register as those interfaces
        var registerAsInterfaces = GetRegisterAsInterfaces(type);

        List<Type> interfaces;
        if (registerAsInterfaces.Count > 0)
        {
            // Only register as explicitly specified interfaces via [RegisterAs<T>]
            interfaces = registerAsInterfaces;
        }
        else
        {
            // Register as interfaces (excluding system interfaces, generic type definitions, decorator interfaces, and IHostedService)
            interfaces = type.GetInterfaces()
                .Where(i => !i.IsGenericTypeDefinition)
                .Where(i => i.Assembly != typeof(object).Assembly) // Skip system interfaces
                .Where(i => !i.Name.StartsWith("System.")) // Additional system interface filtering
                .Where(i => !IsDecoratorInterface(i, constructorParamTypes)) // Skip decorator pattern interfaces
                .Where(i => i.FullName != "Microsoft.Extensions.Hosting.IHostedService") // Hosted services registered separately
                .ToList();
        }

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
    /// Gets interface types specified by [RegisterAs&lt;T&gt;] attributes on the type.
    /// </summary>
    private static List<Type> GetRegisterAsInterfaces(Type type)
    {
        var interfaces = new List<Type>();

        foreach (var attribute in type.GetCustomAttributes(inherit: false))
        {
            var attrType = attribute.GetType();
            if (!attrType.IsGenericType)
                continue;

            var genericTypeDef = attrType.GetGenericTypeDefinition();
            if (genericTypeDef.FullName?.StartsWith("NexusLabs.Needlr.RegisterAsAttribute`1", StringComparison.Ordinal) != true)
                continue;

            // Get the interface type from the generic type argument
            var interfaceType = attrType.GetGenericArguments()[0];
            interfaces.Add(interfaceType);
        }

        return interfaces;
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
