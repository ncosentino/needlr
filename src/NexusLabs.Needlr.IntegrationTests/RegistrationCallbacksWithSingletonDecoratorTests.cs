using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class RegistrationCallbacksWithSingletonDecoratorTests
{
    private readonly IServiceProvider _serviceProvider;

    public RegistrationCallbacksWithSingletonDecoratorTests()
    {
        _serviceProvider = new Syringe()
            .AddPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<MyManualService>();
                services.AddSingleton<IMyManualService, MyManualDecorator>(s =>
                    new MyManualDecorator(s.GetRequiredService<MyManualService>()));
            })
            .BuildServiceProvider();
    }

    [Fact]
    public void GetService_IMyManualService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IMyManualService>());
    }

    [Fact]
    public void GetService_IMyManualService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyManualService>();
        var instance2 = _serviceProvider.GetService<IMyManualService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IMyManualService2_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyManualService2>();
        var instance2 = _serviceProvider.GetService<IMyManualService2>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IMyManualServiceAndMyManualService_AreNotSameInstance()
    {
        var theInterface = _serviceProvider.GetService<IMyManualService>();
        var theImplementation = _serviceProvider.GetService<MyManualService>();
        Assert.NotSame(theInterface, theImplementation);
    }

    [Fact]
    public void GetService_MyManualService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<MyManualService>());
    }

    [Fact]
    public void GetService_MyManualService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<MyManualService>();
        var instance2 = _serviceProvider.GetService<MyManualService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_MyDecorator_Null()
    {
        Assert.Null(_serviceProvider.GetService<MyManualDecorator>());
    }

    [Fact]
    public void GetService_IConfiguration_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IConfiguration>());
    }

    [Fact]
    public void GetService_IConfiguration_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IConfiguration>();
        var instance2 = _serviceProvider.GetService<IConfiguration>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IServiceProvider_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IServiceProvider>());
    }

    [Fact]
    public void GetService_IServiceProvider_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IServiceProvider>();
        var instance2 = _serviceProvider.GetService<IServiceProvider>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetRegisteredTypes_ManuallyRegisteredTypes_ContainsExpectedTypes()
    {
        var allTypes = _serviceProvider.GetRegisteredTypes(type => true);
        
        Assert.Contains(typeof(IMyManualService), allTypes);
        Assert.Contains(typeof(MyManualService), allTypes);
        Assert.Contains(typeof(IConfiguration), allTypes);
        Assert.Contains(typeof(IServiceProvider), allTypes);
    }

    [Fact]
    public void GetRegisteredTypes_InterfaceTypes_ContainsManualInterfaces()
    {
        var interfaceTypes = _serviceProvider.GetRegisteredTypes(type => type.IsInterface);
        
        Assert.Contains(typeof(IMyManualService), interfaceTypes);
        Assert.Contains(typeof(IConfiguration), interfaceTypes);
        Assert.Contains(typeof(IServiceProvider), interfaceTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_IMyManualService_ContainsInterface()
    {
        var manualServiceTypes = _serviceProvider.GetRegisteredTypesOf<IMyManualService>();
        
        Assert.Contains(typeof(IMyManualService), manualServiceTypes);
        // MyManualDecorator is not registered directly, only as a factory for IMyManualService
    }

    [Fact]
    public void GetRegisteredTypesOf_MyManualService_ContainsService()
    {
        var manualServiceTypes = _serviceProvider.GetRegisteredTypesOf<MyManualService>();
        
        Assert.Contains(typeof(MyManualService), manualServiceTypes);
    }

    [Fact]
    public void GetRegisteredTypes_DoesNotContainUnregisteredDecorator()
    {
        var allTypes = _serviceProvider.GetRegisteredTypes(type => true);
        
        // MyManualDecorator should not be directly registered
        Assert.DoesNotContain(typeof(MyManualDecorator), allTypes);
    }

    [Fact]
    public void GetServiceRegistrations_SingletonServices_ContainsManualRegistrations()
    {
        var singletonRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.Lifetime == ServiceLifetime.Singleton);
        
        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(IMyManualService));
        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(MyManualService));
    }

    [Fact]
    public void GetServiceRegistrations_WithFactoryMethods_IdentifiesFactoryRegistrations()
    {
        var factoryRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.ImplementationFactory != null);
        
        // IMyManualService is registered with a factory method
        Assert.Contains(factoryRegistrations, r => r.ServiceType == typeof(IMyManualService) && r.HasFactory);
    }

    [Fact]
    public void GetServiceRegistrations_WithConcreteImplementations_IdentifiesDirectRegistrations()
    {
        var directRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.ImplementationType != null && descriptor.ImplementationFactory == null);
        
        // MyManualService is registered directly
        Assert.Contains(directRegistrations, r => 
            r.ServiceType == typeof(MyManualService) && 
            r.ImplementationType == typeof(MyManualService) &&
            !r.HasFactory);
    }
}
