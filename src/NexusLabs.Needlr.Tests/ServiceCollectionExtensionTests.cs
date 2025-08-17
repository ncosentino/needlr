using Microsoft.Extensions.DependencyInjection;

using System;

using Xunit;

namespace NexusLabs.Needlr.Tests;

public sealed class ServiceCollectionExtensionTests
{
    [Fact]
    public void AddDecorator_WithExistingService_ReplacesWithDecorator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestServiceForDecoration, TestServiceToBeDecorated>();
        services.AddDecorator<ITestServiceForDecoration, TestServiceDecorator>();

        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<ITestServiceForDecoration>();

        Assert.NotNull(service);
        Assert.IsType<TestServiceDecorator>(service);
    }

    [Fact]
    public void AddDecorator_WithoutExistingService_ThrowsException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddDecorator<ITestServiceForDecoration, TestServiceDecorator>();
        });

        Assert.Contains("No service registration found for type ITestService", exception.Message);
    }

    [Fact]
    public void AddDecorator_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() =>
        {
            services!.AddDecorator<ITestServiceForDecoration, TestServiceDecorator>();
        });
    }
}

public interface ITestServiceForDecoration
{
    string DoSomething();
}

[DoNotAutoRegister]
public sealed class TestServiceToBeDecorated : ITestServiceForDecoration
{
    public string DoSomething()
    {
        return "Original";
    }
}

[DoNotAutoRegister]
public sealed class TestServiceDecorator : ITestServiceForDecoration
{
    private readonly ITestServiceForDecoration _wrapped;

    public TestServiceDecorator(ITestServiceForDecoration wrapped)
    {
        _wrapped = wrapped;
    }

    public string DoSomething()
    {
        return $"Decorated: {_wrapped.DoSomething()}";
    }
}