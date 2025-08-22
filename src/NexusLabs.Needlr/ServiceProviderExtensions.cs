using Microsoft.Extensions.DependencyInjection;

using System;

namespace NexusLabs.Needlr;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to provide type inspection functionality.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Gets all registered service types that match the specified predicate without instantiating the services.
    /// </summary>
    /// <param name="serviceProvider">The service provider to inspect.</param>
    /// <param name="predicate">A function to filter the service types.</param>
    /// <returns>A read-only list of types that match the predicate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serviceProvider or predicate is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service collection is not accessible.</exception>
    /// <example>
    /// <code>
    /// // Get all interface types
    /// var interfaceTypes = serviceProvider.GetRegisteredTypes(type => type.IsInterface);
    /// 
    /// // Get all types in a specific namespace
    /// var myNamespaceTypes = serviceProvider.GetRegisteredTypes(type => 
    ///     type.Namespace?.StartsWith("MyApp.Services") == true);
    /// 
    /// // Get all types implementing a specific interface
    /// var repositoryTypes = serviceProvider.GetRegisteredTypes(type => 
    ///     typeof(IRepository).IsAssignableFrom(type));
    /// </code>
    /// </example>
    public static IReadOnlyList<Type> GetRegisteredTypes(
        this IServiceProvider serviceProvider,
        Func<Type, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(predicate);

        // Try to get the service collection from the service provider
        // This works with most DI containers including the default Microsoft container
        var serviceCollection = serviceProvider.GetService<IServiceCollection>()
            ?? throw new InvalidOperationException(
                "Unable to access the service collection from the service provider. " +
                "Ensure the IServiceCollection is registered in the container.");

        return serviceCollection
            .Select(descriptor => descriptor.ServiceType)
            .Distinct()
            .Where(predicate)
            .ToArray();
    }

    /// <summary>
    /// Gets all registered service types that implement or inherit from the specified type without instantiating the services.
    /// </summary>
    /// <typeparam name="T">The base type or interface to filter by.</typeparam>
    /// <param name="serviceProvider">The service provider to inspect.</param>
    /// <returns>A read-only list of types that implement or inherit from T.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serviceProvider is null.</exception>
    /// <example>
    /// <code>
    /// // Get all types implementing IRepository
    /// var repositoryTypes = serviceProvider.GetRegisteredTypesOf&lt;IRepository&gt;();
    /// 
    /// // Get all types inheriting from BaseService
    /// var serviceTypes = serviceProvider.GetRegisteredTypesOf&lt;BaseService&gt;();
    /// </code>
    /// </example>
    public static IReadOnlyList<Type> GetRegisteredTypesOf<T>(this IServiceProvider serviceProvider)
    {
        return serviceProvider
            .GetRegisteredTypes(type => typeof(T)
            .IsAssignableFrom(type));
    }

    /// <summary>
    /// Gets detailed information about all registered services that match the specified predicate.
    /// </summary>
    /// <param name="serviceProvider">The service provider to inspect.</param>
    /// <param name="predicate">A function to filter the service descriptors.</param>
    /// <returns>A read-only list of service registration information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serviceProvider or predicate is null.</exception>
    /// <example>
    /// <code>
    /// // Get all singleton services
    /// var singletons = serviceProvider.GetServiceRegistrations(
    ///     descriptor => descriptor.Lifetime == ServiceLifetime.Singleton);
    /// 
    /// // Get all services with a specific implementation type
    /// var specificImpls = serviceProvider.GetServiceRegistrations(
    ///     descriptor => descriptor.ImplementationType == typeof(MyService));
    /// </code>
    /// </example>
    public static IReadOnlyList<ServiceRegistrationInfo> GetServiceRegistrations(
        this IServiceProvider serviceProvider,
        Func<ServiceDescriptor, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(predicate);

        return serviceProvider
            .GetServiceCollection()
            .Where(predicate)
            .Select(descriptor => new ServiceRegistrationInfo(descriptor))
            .ToArray();
    }

    public static IServiceCollection GetServiceCollection(
        this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return serviceProvider.GetService<IServiceCollection>()
            ?? throw new InvalidOperationException(
                "Unable to access the service collection from the service provider. " +
                "Ensure the IServiceCollection is registered in the container.");
    }

    public static void CopyRegistrationsToServiceCollection(
        this IServiceProvider serviceProvider,
        IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(serviceCollection);

        foreach (var registration in serviceProvider.GetServiceRegistrations(_ => true))
        {
            if (registration.ServiceType == typeof(IServiceProvider))
            {
                continue;
            }

            if (registration.ServiceDescriptor.ServiceType.IsGenericTypeDefinition)
            {
                serviceCollection.Add(registration.ServiceDescriptor);
                continue;
            }

            var descriptor = registration.Lifetime switch
            {
                ServiceLifetime.Singleton => new ServiceDescriptor(
                    registration.ServiceType,
                    _ => serviceProvider.GetRequiredService(registration.ServiceType),
                    ServiceLifetime.Singleton),
                _ => registration.ServiceDescriptor
            };

            serviceCollection.Add(descriptor);
        }
    }

    public static IServiceCollection CopyRegistrationsToServiceCollection(
        this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        ServiceCollection serviceCollection = new();
        serviceProvider.CopyRegistrationsToServiceCollection(serviceCollection);
        return serviceCollection;
    }
}
