using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class SingletonClassAsInterfaceRegistrationCallbacksTests
{
    private readonly IServiceProvider _serviceProvider;

    public SingletonClassAsInterfaceRegistrationCallbacksTests()
    {
        _serviceProvider = new Syringe()
            .AddPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMyManualService, MyManualService>();
            })
            .BuildServiceProvider();
    }

    [Fact]
    public void GetService_IMyService_NotNull()
    {
        Assert.NotNull(_serviceProvider.GetService<IMyManualService>());
    }

    [Fact]
    public void GetService_IMyService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<IMyManualService>();
        var instance2 = _serviceProvider.GetService<IMyManualService>();
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_MyService_Null()
    {
        Assert.Null(_serviceProvider.GetService<MyManualService>());
    }

    [Fact]
    public void GetService_MyService_IsSingleInstance()
    {
        var instance1 = _serviceProvider.GetService<MyManualService>();
        var instance2 = _serviceProvider.GetService<MyManualService>();
        Assert.Same(instance1, instance2);
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
    public void GetRegisteredTypes_InterfaceOnlyRegistration_ContainsInterface()
    {
        var interfaceTypes = _serviceProvider.GetRegisteredTypes(type => type.IsInterface);

        Assert.Contains(typeof(IMyManualService), interfaceTypes);
        Assert.Contains(typeof(IConfiguration), interfaceTypes);
        Assert.Contains(typeof(IServiceProvider), interfaceTypes);
    }

    [Fact]
    public void GetRegisteredTypes_InterfaceOnlyRegistration_DoesNotContainImplementation()
    {
        var allTypes = _serviceProvider.GetRegisteredTypes(type => true);

        Assert.Contains(typeof(IMyManualService), allTypes);
        // MyManualService is not directly registered, only as implementation for the interface
        Assert.DoesNotContain(typeof(MyManualService), allTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_IMyManualService_ContainsInterface()
    {
        var manualServiceTypes = _serviceProvider.GetRegisteredTypesOf<IMyManualService>();

        Assert.Contains(typeof(IMyManualService), manualServiceTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_MyManualService_IsEmpty()
    {
        var manualServiceTypes = _serviceProvider.GetRegisteredTypesOf<MyManualService>();

        // MyManualService is not directly registered, so this should be empty
        Assert.Empty(manualServiceTypes);
    }

    [Fact]
    public void GetServiceRegistrations_InterfaceOnlyRegistration_ContainsCorrectImplementationType()
    {
        var manualServiceRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.ServiceType == typeof(IMyManualService));

        Assert.Single(manualServiceRegistrations);
        var registration = manualServiceRegistrations.First();

        Assert.Equal(typeof(IMyManualService), registration.ServiceType);
        Assert.Equal(typeof(MyManualService), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
        Assert.False(registration.HasFactory);
        Assert.False(registration.HasInstance);
    }

    [Fact]
    public void GetServiceRegistrations_ByImplementationType_FindsInterfaceRegistration()
    {
        var myServiceImplementations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.ImplementationType == typeof(MyManualService));

        Assert.Single(myServiceImplementations);
        var registration = myServiceImplementations.First();

        Assert.Equal(typeof(IMyManualService), registration.ServiceType);
        Assert.Equal(typeof(MyManualService), registration.ImplementationType);
    }
}
