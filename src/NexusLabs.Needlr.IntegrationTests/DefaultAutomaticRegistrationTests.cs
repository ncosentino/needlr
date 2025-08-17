using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class DefaultAutomaticRegistrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public DefaultAutomaticRegistrationTests()
    {
        _serviceProvider = new Syringe().BuildServiceProvider();
    }

    [Fact]
    public void GetService_IMyAutomaticService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IMyAutomaticService>());
    }

    [Fact]
    public void GetService_LazyIMyAutomaticService_NotNull()
    {
        var lazy = _serviceProvider.GetService<Lazy<IMyAutomaticService>>();
        Assert.NotNull(lazy);
        Assert.NotNull(lazy.Value);
    }

    [Fact]
    public void GetService_IMyAutomaticService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyAutomaticService>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_IMyAutomaticService2_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyAutomaticService2>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService2>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_LazyIMyAutomaticService2_NotNull()
    {
        var lazy = _serviceProvider.GetService<Lazy<IMyAutomaticService2>>();
        Assert.NotNull(lazy);
        Assert.NotNull(lazy.Value);
    }

    [Fact]
    public void GetService_IMyAutomaticService1And2_AreSame()
    {
        var instance1 = _serviceProvider.GetService<IMyAutomaticService>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService2>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_MyAutomaticService_Null()
    {
        Assert.NotNull(_serviceProvider.GetService<MyAutomaticService>());
    }

    [Fact]
    public void GetService_MyAutomaticService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<MyAutomaticService>();
        var instance2 = _serviceProvider.GetService<MyAutomaticService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_MyAutomaticServiceAndInterface_AreSame()
    {
        var instance1 = _serviceProvider.GetService<MyAutomaticService>();
        var instance2 = _serviceProvider.GetService<IMyAutomaticService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_LazyMyAutomaticService_NotNull()
    {
        var lazy = _serviceProvider.GetService<Lazy<MyAutomaticService>>();
        Assert.NotNull(lazy);
        Assert.NotNull(lazy.Value);
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
    public void GetService_IInterfaceWithMultipleImplementations_ResolvesAsEnumerable()
    {
        var instances = _serviceProvider.GetService<IEnumerable<IInterfaceWithMultipleImplementations>>();
        Assert.NotNull(instances);
        Assert.NotEmpty(instances);
    }

    [Fact]
    public void GetService_IInterfaceWithMultipleImplementations_ResolvesAsIReadOnlyList()
    {
        var instances = _serviceProvider.GetService<IReadOnlyList<IInterfaceWithMultipleImplementations>>();
        Assert.NotNull(instances);
        Assert.NotEmpty(instances);
    }

    [Fact]
    public void GetService_IInterfaceWithMultipleImplementations_ResolvesAsIReadOnlyCollection()
    {
        var instances = _serviceProvider.GetService<IReadOnlyCollection<IInterfaceWithMultipleImplementations>>();
        Assert.NotNull(instances);
        Assert.NotEmpty(instances);
    }

    [Fact]
    public void GetRegisteredTypes_AllInterfaces_ContainsExpectedTypes()
    {
        var interfaceTypes = _serviceProvider.GetRegisteredTypes(type => type.IsInterface);
        
        Assert.Contains(typeof(IMyAutomaticService), interfaceTypes);
        Assert.Contains(typeof(IMyAutomaticService2), interfaceTypes);
        Assert.Contains(typeof(IInterfaceWithMultipleImplementations), interfaceTypes);
        Assert.Contains(typeof(IConfiguration), interfaceTypes);
        Assert.Contains(typeof(IServiceProvider), interfaceTypes);
    }

    [Fact]
    public void GetRegisteredTypes_AllClasses_ContainsExpectedTypes()
    {
        var classTypes = _serviceProvider.GetRegisteredTypes(type => type.IsClass && !type.IsAbstract);
        
        Assert.Contains(typeof(MyAutomaticService), classTypes);
        Assert.Contains(typeof(ImplementationA), classTypes);
        Assert.Contains(typeof(ImplementationB), classTypes);
    }

    [Fact]
    public void GetRegisteredTypes_TypesInTestNamespace_ContainsExpectedTypes()
    {
        var testNamespaceTypes = _serviceProvider.GetRegisteredTypes(type => 
            type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true);
        
        Assert.Contains(typeof(MyAutomaticService), testNamespaceTypes);
        Assert.Contains(typeof(IMyAutomaticService), testNamespaceTypes);
        Assert.Contains(typeof(IMyAutomaticService2), testNamespaceTypes);
        Assert.Contains(typeof(ImplementationA), testNamespaceTypes);
        Assert.Contains(typeof(ImplementationB), testNamespaceTypes);
        Assert.Contains(typeof(IInterfaceWithMultipleImplementations), testNamespaceTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_IMyAutomaticService_ContainsExpectedTypes()
    {
        var automaticServiceTypes = _serviceProvider.GetRegisteredTypesOf<IMyAutomaticService>();
        
        Assert.Contains(typeof(IMyAutomaticService), automaticServiceTypes);
        Assert.Contains(typeof(MyAutomaticService), automaticServiceTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_IMyAutomaticService2_ContainsExpectedTypes()
    {
        var automaticService2Types = _serviceProvider.GetRegisteredTypesOf<IMyAutomaticService2>();
        
        Assert.Contains(typeof(IMyAutomaticService2), automaticService2Types);
        Assert.Contains(typeof(MyAutomaticService), automaticService2Types);
    }

    [Fact]
    public void GetRegisteredTypesOf_IInterfaceWithMultipleImplementations_ContainsAllImplementations()
    {
        var multipleImplTypes = _serviceProvider.GetRegisteredTypesOf<IInterfaceWithMultipleImplementations>();
        
        Assert.Contains(typeof(IInterfaceWithMultipleImplementations), multipleImplTypes);
        Assert.Contains(typeof(ImplementationA), multipleImplTypes);
        Assert.Contains(typeof(ImplementationB), multipleImplTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_Object_ContainsAllRegisteredClasses()
    {
        var objectTypes = _serviceProvider.GetRegisteredTypesOf<object>();
        
        // Should contain all registered classes since everything inherits from object
        Assert.Contains(typeof(MyAutomaticService), objectTypes);
        Assert.Contains(typeof(ImplementationA), objectTypes);
        Assert.Contains(typeof(ImplementationB), objectTypes);
    }

    [Fact]
    public void GetServiceRegistrations_SingletonServices_ContainsExpectedRegistrations()
    {
        var singletonRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.Lifetime == ServiceLifetime.Singleton);
        
        // Verify some expected singleton registrations exist
        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(IMyAutomaticService));
        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(IMyAutomaticService2));
        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(MyAutomaticService));
        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(IConfiguration));
        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(IServiceProvider));
    }

    [Fact]
    public void GetServiceRegistrations_MyAutomaticServiceImplementationType_ContainsExpectedRegistrations()
    {
        var myServiceRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.ImplementationType == typeof(MyAutomaticService));
        
        // Should have registrations for both the class and its interfaces with MyAutomaticService as implementation
        Assert.Contains(myServiceRegistrations, r => r.ServiceType == typeof(MyAutomaticService));
        Assert.All(myServiceRegistrations, r => 
        {
            Assert.Equal(typeof(MyAutomaticService), r.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, r.Lifetime);
        });
    }
}
