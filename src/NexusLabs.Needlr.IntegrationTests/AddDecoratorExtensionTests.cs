using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class AddDecoratorExtensionTests
{
    private readonly IServiceProvider _serviceProvider;

    public AddDecoratorExtensionTests()
    {
        _serviceProvider = new Syringe()
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
}