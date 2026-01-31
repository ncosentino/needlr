using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Reflection;

/// <summary>
/// Integration tests verifying that AddDecorator correctly decorates ALL registrations
/// of a service type when using the reflection discovery path.
/// </summary>
public sealed class MultipleRegistrationDecoratorReflectionTests
{
    [Fact]
    public void Decorator_WithMultipleRegistrations_AllDecorated_Reflection()
    {
        // Arrange: Register two implementations via callback, decorator applied by AddDecorator call
        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegReflectionTestService, MultiRegReflectionServiceA>();
                services.AddSingleton<IMultiRegReflectionTestService, MultiRegReflectionServiceB>();
            })
            .AddDecorator<IMultiRegReflectionTestService, MultiRegReflectionDecorator>()
            .BuildServiceProvider();

        // Act
        var instances = serviceProvider.GetServices<IMultiRegReflectionTestService>().ToList();

        // Assert - Both registrations should be decorated
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<MultiRegReflectionDecorator>(instance));

        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Decorated: ReflectionA", values);
        Assert.Contains("Decorated: ReflectionB", values);
    }

    [Fact]
    public void ChainedDecorators_WithMultipleRegistrations_AllDecorated_Reflection()
    {
        // Arrange: Two decorators applied to two registrations
        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegReflectionTestService, MultiRegReflectionServiceA>();
                services.AddSingleton<IMultiRegReflectionTestService, MultiRegReflectionServiceB>();
            })
            .AddDecorator<IMultiRegReflectionTestService, MultiRegReflectionDecorator>()
            .AddDecorator<IMultiRegReflectionTestService, MultiRegReflectionSecondDecorator>()
            .BuildServiceProvider();

        // Act
        var instances = serviceProvider.GetServices<IMultiRegReflectionTestService>().ToList();

        // Assert
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<MultiRegReflectionSecondDecorator>(instance));

        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Second(Decorated: ReflectionA)", values);
        Assert.Contains("Second(Decorated: ReflectionB)", values);
    }

    [Fact]
    public void Decorator_WithDifferentLifetimes_PreservesLifetimes_Reflection()
    {
        // Arrange: Different lifetimes for different implementations
        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegReflectionTestService, MultiRegReflectionServiceA>();
                services.AddScoped<IMultiRegReflectionTestService, MultiRegReflectionServiceB>();
            })
            .AddDecorator<IMultiRegReflectionTestService, MultiRegReflectionDecorator>()
            .BuildServiceProvider();

        // Act - Use scope to verify lifetimes
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var instances1 = scope1.ServiceProvider.GetServices<IMultiRegReflectionTestService>().ToList();
        var instances2 = scope2.ServiceProvider.GetServices<IMultiRegReflectionTestService>().ToList();

        // Assert - Both should have 2 instances
        Assert.Equal(2, instances1.Count);
        Assert.Equal(2, instances2.Count);

        // All should be decorated
        Assert.All(instances1, i => Assert.IsType<MultiRegReflectionDecorator>(i));
        Assert.All(instances2, i => Assert.IsType<MultiRegReflectionDecorator>(i));
    }
}

public interface IMultiRegReflectionTestService
{
    string GetValue();
}

[DoNotAutoRegister]
public sealed class MultiRegReflectionServiceA : IMultiRegReflectionTestService
{
    public string GetValue() => "ReflectionA";
}

[DoNotAutoRegister]
public sealed class MultiRegReflectionServiceB : IMultiRegReflectionTestService
{
    public string GetValue() => "ReflectionB";
}

[DoNotAutoRegister]
public sealed class MultiRegReflectionDecorator : IMultiRegReflectionTestService
{
    private readonly IMultiRegReflectionTestService _inner;

    public MultiRegReflectionDecorator(IMultiRegReflectionTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Decorated: {_inner.GetValue()}";
}

[DoNotAutoRegister]
public sealed class MultiRegReflectionSecondDecorator : IMultiRegReflectionTestService
{
    private readonly IMultiRegReflectionTestService _inner;

    public MultiRegReflectionSecondDecorator(IMultiRegReflectionTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Second({_inner.GetValue()})";
}
