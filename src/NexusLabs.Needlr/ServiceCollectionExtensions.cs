using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to provide decorator functionality.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Decorates an existing service registration with a decorator type, preserving the original service's lifetime.
    /// The decorator must implement the service interface and take the service interface as a constructor parameter.
    /// Works with both interfaces and class types.
    /// </summary>
    /// <typeparam name="TService">The service type (interface or class) to decorate.</typeparam>
    /// <typeparam name="TDecorator">The decorator type that implements TService.</typeparam>
    /// <param name="services">The service collection to modify.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no service registration is found for TService.</exception>
    /// <example>
    /// <code>
    /// // Register the original service
    /// services.AddScoped&lt;IMyService, MyService&gt;();
    /// 
    /// // Decorate it while preserving the scoped lifetime
    /// services.AddDecorator&lt;IMyService, MyServiceDecorator&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddDecorator<TService, TDecorator>(this IServiceCollection services)
        where TDecorator : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        // Find the existing service registration
        var existingDescriptor = services
            .LastOrDefault(d => d.ServiceType == typeof(TService)) 
            ?? throw new InvalidOperationException(
                $"No service registration found for type {typeof(TService).Name}. " +
                $"Please register the service before decorating it.");
        services.Remove(existingDescriptor);

        // Create a new registration with the same lifetime that uses the decorator
        var decoratedDescriptor = existingDescriptor.Lifetime switch
        {
            ServiceLifetime.Singleton => new ServiceDescriptor(typeof(TService), provider =>
            {
                var originalService = CreateOriginalService<TService>(provider, existingDescriptor);
                return ActivatorUtilities.CreateInstance<TDecorator>(provider, originalService!);
            }, ServiceLifetime.Singleton),
            ServiceLifetime.Scoped => new ServiceDescriptor(typeof(TService), provider =>
            {
                var originalService = CreateOriginalService<TService>(provider, existingDescriptor);
                return ActivatorUtilities.CreateInstance<TDecorator>(provider, originalService!);
            }, ServiceLifetime.Scoped),
            ServiceLifetime.Transient => new ServiceDescriptor(typeof(TService), provider =>
            {
                var originalService = CreateOriginalService<TService>(provider, existingDescriptor);
                return ActivatorUtilities.CreateInstance<TDecorator>(provider, originalService!);
            }, ServiceLifetime.Transient),
            _ => throw new InvalidOperationException(
                $"Unsupported service lifetime '{existingDescriptor.Lifetime}' " +
                $"for '{typeof(TService)}'.")
        };

        services.Add(decoratedDescriptor);
        return services;
    }

    /// <summary>
    /// Gets detailed information about all registered services.
    /// </summary>
    /// <param name="serviceCollection">The service provider to inspect.</param>
    /// <returns>A read-only list of service registration information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serviceCollection is null.</exception>
    /// <example>
    /// <code>
    /// // Get all singleton services
    /// var singletons = serviceCollection.GetServiceRegistrations(
    ///     descriptor => descriptor.Lifetime == ServiceLifetime.Singleton);
    /// 
    /// // Get all services with a specific implementation type
    /// var specificImpls = serviceCollection.GetServiceRegistrations(
    ///     descriptor => descriptor.ImplementationType == typeof(MyService));
    /// </code>
    /// </example>
    public static IReadOnlyList<ServiceRegistrationInfo> GetServiceRegistrations(
        this IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        return serviceCollection.GetServiceRegistrations(_ => true);
    }

    /// <summary>
    /// Gets detailed information about all registered services that match the specified predicate.
    /// </summary>
    /// <param name="serviceCollection">The service provider to inspect.</param>
    /// <param name="predicate">A function to filter the service descriptors.</param>
    /// <returns>A read-only list of service registration information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serviceCollection or predicate is null.</exception>
    /// <example>
    /// <code>
    /// // Get all singleton services
    /// var singletons = serviceCollection.GetServiceRegistrations(
    ///     descriptor => descriptor.Lifetime == ServiceLifetime.Singleton);
    /// 
    /// // Get all services with a specific implementation type
    /// var specificImpls = serviceCollection.GetServiceRegistrations(
    ///     descriptor => descriptor.ImplementationType == typeof(MyService));
    /// </code>
    /// </example>
    public static IReadOnlyList<ServiceRegistrationInfo> GetServiceRegistrations(
        this IServiceCollection serviceCollection,
        Func<ServiceDescriptor, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(predicate);

        return serviceCollection
            .Where(predicate)
            .Select(descriptor => new ServiceRegistrationInfo(descriptor))
            .ToArray();
    }

    public static bool IsRegistered<TService>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.IsRegistered(typeof(TService));
    }

    public static bool IsRegistered(
        this IServiceCollection services,
        Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Any(d => d.ServiceType == serviceType);
    }

    private static TService CreateOriginalService<TService>(IServiceProvider provider, ServiceDescriptor originalDescriptor)
    {
        if (originalDescriptor.ImplementationFactory is not null)
        {
            return (TService)originalDescriptor.ImplementationFactory(provider);
        }
        
        if (originalDescriptor.ImplementationInstance is not null)
        {
            return (TService)originalDescriptor.ImplementationInstance;
        }
        
        if (originalDescriptor.ImplementationType is not null)
        {
            return (TService)ActivatorUtilities.CreateInstance(provider, originalDescriptor.ImplementationType);
        }
        
        throw new InvalidOperationException($"Unable to create instance of service {typeof(TService).Name} from the original descriptor.");
    }
}
