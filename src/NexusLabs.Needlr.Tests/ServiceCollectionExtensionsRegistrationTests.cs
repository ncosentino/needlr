using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for GetServiceRegistrations and IsRegistered extension methods.
/// </summary>
public sealed class ServiceCollectionExtensionsRegistrationTests
{
    #region GetServiceRegistrations Tests

    [Fact]
    public void GetServiceRegistrations_WithNoParameters_ReturnsAllRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddTransient<ITransientService, TransientService>();

        var registrations = services.GetServiceRegistrations();

        Assert.Equal(3, registrations.Count);
        Assert.Contains(registrations, r => r.ServiceType == typeof(ISingletonService));
        Assert.Contains(registrations, r => r.ServiceType == typeof(IScopedService));
        Assert.Contains(registrations, r => r.ServiceType == typeof(ITransientService));
    }

    [Fact]
    public void GetServiceRegistrations_WithNullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.GetServiceRegistrations());
    }

    [Fact]
    public void GetServiceRegistrations_WithPredicate_FiltersCorrectly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddTransient<ITransientService, TransientService>();

        var singletons = services.GetServiceRegistrations(
            d => d.Lifetime == ServiceLifetime.Singleton);

        Assert.Single(singletons);
        Assert.Equal(typeof(ISingletonService), singletons[0].ServiceType);
    }

    [Fact]
    public void GetServiceRegistrations_FilterByLifetime_ReturnsMatchingOnly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddScoped<IAnotherScoped, AnotherScoped>();
        services.AddTransient<ITransientService, TransientService>();

        var scopedRegistrations = services.GetServiceRegistrations(
            d => d.Lifetime == ServiceLifetime.Scoped);

        Assert.Equal(2, scopedRegistrations.Count);
        Assert.All(scopedRegistrations, r => Assert.Equal(ServiceLifetime.Scoped, r.Lifetime));
    }

    [Fact]
    public void GetServiceRegistrations_FilterByImplementationType_ReturnsMatchingOnly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();
        services.AddScoped<IScopedService, ScopedService>();

        var registrations = services.GetServiceRegistrations(
            d => d.ImplementationType == typeof(SingletonService));

        Assert.Single(registrations);
        Assert.Equal(typeof(SingletonService), registrations[0].ImplementationType);
    }

    [Fact]
    public void GetServiceRegistrations_WithPredicateNullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.GetServiceRegistrations(d => true));
    }

    [Fact]
    public void GetServiceRegistrations_WithNullPredicate_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.GetServiceRegistrations(null!));
    }

    [Fact]
    public void GetServiceRegistrations_WithEmptyCollection_ReturnsEmptyList()
    {
        var services = new ServiceCollection();

        var registrations = services.GetServiceRegistrations();

        Assert.Empty(registrations);
    }

    [Fact]
    public void GetServiceRegistrations_WithNoMatches_ReturnsEmptyList()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();

        var registrations = services.GetServiceRegistrations(
            d => d.Lifetime == ServiceLifetime.Transient);

        Assert.Empty(registrations);
    }

    #endregion

    #region IsRegistered Tests

    [Fact]
    public void IsRegistered_Generic_WithRegisteredService_ReturnsTrue()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();

        var result = services.IsRegistered<ISingletonService>();

        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_Generic_WithUnregisteredService_ReturnsFalse()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();

        var result = services.IsRegistered<IScopedService>();

        Assert.False(result);
    }

    [Fact]
    public void IsRegistered_Generic_WithNullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.IsRegistered<ISingletonService>());
    }

    [Fact]
    public void IsRegistered_Generic_WithEmptyCollection_ReturnsFalse()
    {
        var services = new ServiceCollection();

        var result = services.IsRegistered<ISingletonService>();

        Assert.False(result);
    }

    [Fact]
    public void IsRegistered_NonGeneric_WithRegisteredService_ReturnsTrue()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();

        var result = services.IsRegistered(typeof(ISingletonService));

        Assert.True(result);
    }

    [Fact]
    public void IsRegistered_NonGeneric_WithUnregisteredService_ReturnsFalse()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();

        var result = services.IsRegistered(typeof(IScopedService));

        Assert.False(result);
    }

    [Fact]
    public void IsRegistered_NonGeneric_WithNullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.IsRegistered(typeof(ISingletonService)));
    }

    [Fact]
    public void IsRegistered_WithMultipleRegistrationsSameType_ReturnsTrue()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();
        services.AddSingleton<ISingletonService, AnotherSingletonService>();

        var result = services.IsRegistered<ISingletonService>();

        Assert.True(result);
    }

    #endregion

    #region ServiceRegistrationInfo Tests

    [Fact]
    public void ServiceRegistrationInfo_ContainsCorrectServiceDescriptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonService>();

        var registrations = services.GetServiceRegistrations();

        Assert.Single(registrations);
        Assert.NotNull(registrations[0].ServiceDescriptor);
        Assert.Equal(typeof(ISingletonService), registrations[0].ServiceDescriptor.ServiceType);
    }

    [Fact]
    public void ServiceRegistrationInfo_ReportsCorrectLifetime()
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();

        var registrations = services.GetServiceRegistrations();

        Assert.Single(registrations);
        Assert.Equal(ServiceLifetime.Scoped, registrations[0].Lifetime);
    }

    [Fact]
    public void ServiceRegistrationInfo_ReportsCorrectImplementationType()
    {
        var services = new ServiceCollection();
        services.AddTransient<ITransientService, TransientService>();

        var registrations = services.GetServiceRegistrations();

        Assert.Single(registrations);
        Assert.Equal(typeof(TransientService), registrations[0].ImplementationType);
    }

    #endregion

    #region Test Types

    public interface ISingletonService { }
    public interface IScopedService { }
    public interface IAnotherScoped { }
    public interface ITransientService { }

    [DoNotAutoRegister]
    public sealed class SingletonService : ISingletonService { }

    [DoNotAutoRegister]
    public sealed class AnotherSingletonService : ISingletonService { }

    [DoNotAutoRegister]
    public sealed class ScopedService : IScopedService { }

    [DoNotAutoRegister]
    public sealed class AnotherScoped : IAnotherScoped { }

    [DoNotAutoRegister]
    public sealed class TransientService : ITransientService { }

    #endregion
}
