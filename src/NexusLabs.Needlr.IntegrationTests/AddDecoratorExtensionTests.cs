using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class AddDecoratorExtensionTests
{
    private readonly IServiceProvider _serviceProvider;

    public AddDecoratorExtensionTests()
    {
        _serviceProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                // Register the base service
                services.AddSingleton<ITestServiceForDecoration, TestServiceToBeDecorated>();
            })
            .AddDecorator<ITestServiceForDecoration, TestServiceDecorator>()
            .BuildServiceProvider();
    }

    [Fact]
    public void GetService_ITestService_NotNull()
    {
        var service = _serviceProvider.GetService<ITestServiceForDecoration>();
        Assert.NotNull(service);
    }

    [Fact]
    public void GetService_ITestService_IsDecorated()
    {
        var service = _serviceProvider.GetService<ITestServiceForDecoration>();
        Assert.IsType<TestServiceDecorator>(service);
    }

    [Fact]
    public void GetService_ITestService_PreservesLifetime()
    {
        var instance1 = _serviceProvider.GetService<ITestServiceForDecoration>();
        var instance2 = _serviceProvider.GetService<ITestServiceForDecoration>();

        // Since we registered as singleton, they should be the same instance
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetService_ITestService_DecoratorFunctionality()
    {
        var service = _serviceProvider.GetService<ITestServiceForDecoration>();
        var result = service?.DoSomething();
        Assert.Equal("Decorated: Original", result);
    }

    [Fact]
    public void GetRegisteredTypes_DecoratedService_ContainsInterface()
    {
        var interfaceTypes = _serviceProvider.GetRegisteredTypes(type => type.IsInterface);

        Assert.Contains(typeof(ITestServiceForDecoration), interfaceTypes);
    }

    [Fact]
    public void GetRegisteredTypesOf_ITestServiceForDecoration_ContainsInterface()
    {
        var decorationServiceTypes = _serviceProvider.GetRegisteredTypesOf<ITestServiceForDecoration>();

        Assert.Contains(typeof(ITestServiceForDecoration), decorationServiceTypes);
    }

    [Fact]
    public void GetRegisteredTypes_DoesNotContainConcreteDecoratorType()
    {
        var allTypes = _serviceProvider.GetRegisteredTypes(type => true);

        // The concrete decorator class should not be directly registered
        Assert.DoesNotContain(typeof(TestServiceDecorator), allTypes);
        // The concrete decorated service should not be directly registered either
        Assert.DoesNotContain(typeof(TestServiceToBeDecorated), allTypes);
    }

    [Fact]
    public void GetServiceRegistrations_DecoratedService_UsesFactory()
    {
        var decoratedServiceRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.ServiceType == typeof(ITestServiceForDecoration));

        // There should be exactly one registration for the interface
        Assert.Single(decoratedServiceRegistrations);

        var registration = decoratedServiceRegistrations.First();
        Assert.True(registration.HasFactory, "Decorated service should be registered with a factory");
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public void GetServiceRegistrations_SingletonServices_ContainsDecoratedService()
    {
        var singletonRegistrations = _serviceProvider.GetServiceRegistrations(
            descriptor => descriptor.Lifetime == ServiceLifetime.Singleton);

        Assert.Contains(singletonRegistrations, r => r.ServiceType == typeof(ITestServiceForDecoration));
    }
}