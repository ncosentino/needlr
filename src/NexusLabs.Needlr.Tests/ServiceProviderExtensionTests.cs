using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

public sealed class ServiceProviderExtensionTests
{
    private static IServiceProvider CreateServiceProviderWithServiceCollection(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        
        services.AddSingleton<IServiceCollection>(services);
        var actualProvider = services.BuildServiceProvider();
        return actualProvider;
    }

    [Fact]
    public void GetRegisteredTypes_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.GetRegisteredTypes(type => true));
    }

    [Fact]
    public void GetRegisteredTypes_WithNullPredicate_ThrowsArgumentNullException()
    {
        var serviceProvider = CreateServiceProviderWithServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            serviceProvider.GetRegisteredTypes(null!));
    }

    [Fact]
    public void GetRegisteredTypesOf_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.GetRegisteredTypesOf<object>());
    }

    [Fact]
    public void GetServiceRegistrations_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.GetServiceRegistrations(descriptor => true));
    }

    [Fact]
    public void GetServiceRegistrations_WithNullPredicate_ThrowsArgumentNullException()
    {
        var serviceProvider = CreateServiceProviderWithServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            serviceProvider.GetServiceRegistrations(null!));
    }

    [Fact]
    public void GetRegisteredTypes_WithServiceProviderWithoutServiceCollection_ThrowsInvalidOperationException()
    {
        // Create a minimal service provider without IServiceCollection registered
        var services = new ServiceCollection();
        services.AddSingleton<string>("test");
        var providerWithoutCollection = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            providerWithoutCollection.GetRegisteredTypes(type => true));

        Assert.Contains("Unable to access the service collection", exception.Message);
        Assert.Contains("Ensure the IServiceCollection is registered", exception.Message);
    }

    [Fact]
    public void GetServiceRegistrations_WithServiceProviderWithoutServiceCollection_ThrowsInvalidOperationException()
    {
        // Create a minimal service provider without IServiceCollection registered
        var services = new ServiceCollection();
        services.AddSingleton<string>("test");
        var providerWithoutCollection = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            providerWithoutCollection.GetServiceRegistrations(descriptor => true));

        Assert.Contains("Unable to access the service collection", exception.Message);
        Assert.Contains("Ensure the IServiceCollection is registered", exception.Message);
    }


    [Fact]
    public void GetServiceCollection_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.GetServiceCollection());
    }

    [Fact]
    public void GetServiceCollection_WithValidServiceProvider_ReturnsServiceCollection()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddSingleton<string>("test");
        });

        var result = provider.GetServiceCollection();

        Assert.NotNull(result);
        Assert.IsAssignableFrom<IServiceCollection>(result);
    }

    [Fact]
    public void GetServiceCollection_WithServiceProviderWithoutServiceCollection_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<string>("test");
        var providerWithoutCollection = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            providerWithoutCollection.GetServiceCollection());

        Assert.Contains("Unable to access the service collection", exception.Message);
        Assert.Contains("Ensure the IServiceCollection is registered", exception.Message);
    }


    [Fact]
    public void CopyRegistrationsToServiceCollection_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;
        var targetCollection = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.CopyRegistrationsToServiceCollection(targetCollection));
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithNullTargetCollection_ThrowsArgumentNullException()
    {
        IServiceCollection nullCollection = null!;

        var serviceProvider = CreateServiceProviderWithServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            serviceProvider.CopyRegistrationsToServiceCollection(nullCollection));
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithTransientServices_CopiesDescriptorDirectly()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddTransient<ITestService, TestService>();
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var copiedService = targetCollection.FirstOrDefault(d => d.ServiceType == typeof(ITestService));
        Assert.NotNull(copiedService);
        Assert.Equal(ServiceLifetime.Transient, copiedService.Lifetime);
        Assert.Equal(typeof(TestService), copiedService.ImplementationType);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithScopedServices_CopiesDescriptorDirectly()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddScoped<ITestService, TestService>();
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var copiedService = targetCollection.FirstOrDefault(d => d.ServiceType == typeof(ITestService));
        Assert.NotNull(copiedService);
        Assert.Equal(ServiceLifetime.Scoped, copiedService.Lifetime);
        Assert.Equal(typeof(TestService), copiedService.ImplementationType);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithSingletonServices_CreatesFactoryDescriptor()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddSingleton<ITestService, TestService>();
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var copiedService = targetCollection.FirstOrDefault(d => d.ServiceType == typeof(ITestService));
        Assert.NotNull(copiedService);
        Assert.Equal(ServiceLifetime.Singleton, copiedService.Lifetime);
        Assert.NotNull(copiedService.ImplementationFactory);
        Assert.Null(copiedService.ImplementationType);
        Assert.Null(copiedService.ImplementationInstance);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithGenericTypeDefinitions_CopiesDescriptorDirectly()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddTransient(typeof(IGenericService<>), typeof(GenericService<>));
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var copiedService = targetCollection.FirstOrDefault(d => d.ServiceType == typeof(IGenericService<>));
        Assert.NotNull(copiedService);
        Assert.Equal(ServiceLifetime.Transient, copiedService.Lifetime);
        Assert.Equal(typeof(GenericService<>), copiedService.ImplementationType);
        Assert.True(copiedService.ServiceType.IsGenericTypeDefinition);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_ExcludesIServiceProviderRegistrations()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddSingleton<ITestService, TestService>();
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var serviceProviderRegistration = targetCollection.FirstOrDefault(d => d.ServiceType == typeof(IServiceProvider));
        Assert.Null(serviceProviderRegistration);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithMixedServiceLifetimes_CopiesAllCorrectly()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddTransient<ITestService, TestService>();
            services.AddScoped<IAnotherService, AnotherService>();
            services.AddSingleton<IThirdService, ThirdService>();
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        Assert.Equal(3, targetCollection.Count(d => d.ServiceType != typeof(IServiceCollection) && d.ServiceType != typeof(IServiceProvider)));

        var transientService = targetCollection.First(d => d.ServiceType == typeof(ITestService));
        Assert.Equal(ServiceLifetime.Transient, transientService.Lifetime);
        Assert.Equal(typeof(TestService), transientService.ImplementationType);

        var scopedService = targetCollection.First(d => d.ServiceType == typeof(IAnotherService));
        Assert.Equal(ServiceLifetime.Scoped, scopedService.Lifetime);
        Assert.Equal(typeof(AnotherService), scopedService.ImplementationType);

        var singletonService = targetCollection.First(d => d.ServiceType == typeof(IThirdService));
        Assert.Equal(ServiceLifetime.Singleton, singletonService.Lifetime);
        Assert.NotNull(singletonService.ImplementationFactory);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithServicesRegisteredWithFactory_CopiesCorrectly()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddTransient<ITestService>(provider => new TestService());
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var copiedService = targetCollection.FirstOrDefault(d => d.ServiceType == typeof(ITestService));
        Assert.NotNull(copiedService);
        Assert.Equal(ServiceLifetime.Transient, copiedService.Lifetime);
        Assert.NotNull(copiedService.ImplementationFactory);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithServicesRegisteredWithInstance_CopiesCorrectly()
    {
        var instance = new TestService();
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddSingleton<ITestService>(instance);
        });
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var copiedService = targetCollection.FirstOrDefault(d => d.ServiceType == typeof(ITestService));
        Assert.NotNull(copiedService);
        Assert.Equal(ServiceLifetime.Singleton, copiedService.Lifetime);
        Assert.NotNull(copiedService.ImplementationFactory);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_WithEmptyServiceProvider_DoesNotAddAnyExtraServices()
    {
        var provider = CreateServiceProviderWithServiceCollection();
        var targetCollection = new ServiceCollection();

        provider.CopyRegistrationsToServiceCollection(targetCollection);

        var service = Assert.Single(targetCollection);
        Assert.Equal(typeof(IServiceCollection), service.ServiceType);
    }


    [Fact]
    public void CopyRegistrationsToServiceCollection_ParameterlessOverload_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider nullProvider = null!;

        Assert.Throws<ArgumentNullException>(() =>
            nullProvider.CopyRegistrationsToServiceCollection());
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_ParameterlessOverload_ReturnsNewServiceCollection()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddTransient<ITestService, TestService>();
            services.AddScoped<IAnotherService, AnotherService>();
        });

        var result = provider.CopyRegistrationsToServiceCollection();

        Assert.NotNull(result);
        Assert.IsType<ServiceCollection>(result);
        Assert.Equal(2, result.Count(d => d.ServiceType != typeof(IServiceCollection) && d.ServiceType != typeof(IServiceProvider)));
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_ParameterlessOverload_CopiesAllRegistrations()
    {
        var provider = CreateServiceProviderWithServiceCollection(services =>
        {
            services.AddTransient<ITestService, TestService>();
            services.AddSingleton<IAnotherService, AnotherService>();
        });

        var result = provider.CopyRegistrationsToServiceCollection();

        var transientService = result.FirstOrDefault(d => d.ServiceType == typeof(ITestService));
        Assert.NotNull(transientService);
        Assert.Equal(ServiceLifetime.Transient, transientService.Lifetime);

        var singletonService = result.FirstOrDefault(d => d.ServiceType == typeof(IAnotherService));
        Assert.NotNull(singletonService);
        Assert.Equal(ServiceLifetime.Singleton, singletonService.Lifetime);
        Assert.NotNull(singletonService.ImplementationFactory);
    }

    [Fact]
    public void CopyRegistrationsToServiceCollection_ParameterlessOverload_WithEmptyProvider_DoesNotAddExtraServices()
    {
        var provider = CreateServiceProviderWithServiceCollection();

        var result = provider.CopyRegistrationsToServiceCollection();

        var service = Assert.Single(result);
        Assert.Equal(typeof(IServiceCollection), service.ServiceType);
    }

    public interface ITestService { }
    public interface IAnotherService { }
    public interface IThirdService { }
    public interface IGenericService<T> { }

    public class TestService : ITestService { }
    public class AnotherService : IAnotherService { }
    public class ThirdService : IThirdService { }
    public class GenericService<T> : IGenericService<T> { }
}
