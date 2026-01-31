using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify decorators are applied to ALL registrations of the same
/// service type, not just the last one. This tests the fix for the AddDecorator bug
/// where only the last registration was decorated.
/// </summary>
public sealed class MultipleRegistrationDecoratorParityTests
{
    [Fact]
    public void Decorator_WithMultipleRegistrations_AllDecorated_Reflection()
    {
        // Arrange: Register two implementations of the same interface, then apply decorator
        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddSingleton<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .BuildServiceProvider();

        // Act
        var instances = serviceProvider.GetServices<IMultiRegTestService>().ToList();

        // Assert
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<MultiRegDecorator>(instance));

        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Decorated: ServiceA", values);
        Assert.Contains("Decorated: ServiceB", values);
    }

    [Fact]
    public void Decorator_WithMultipleRegistrations_AllDecorated_SourceGen()
    {
        // Arrange: Register two implementations of the same interface, then apply decorator
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddSingleton<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .BuildServiceProvider();

        // Act
        var instances = serviceProvider.GetServices<IMultiRegTestService>().ToList();

        // Assert
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<MultiRegDecorator>(instance));

        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Decorated: ServiceA", values);
        Assert.Contains("Decorated: ServiceB", values);
    }

    [Fact]
    public void Decorator_WithMultipleRegistrations_Parity_ReflectionMatchesSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddSingleton<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddSingleton<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .BuildServiceProvider();

        // Act
        var reflectionInstances = reflectionProvider.GetServices<IMultiRegTestService>().ToList();
        var sourceGenInstances = sourceGenProvider.GetServices<IMultiRegTestService>().ToList();

        // Assert - Both paths should have same count
        Assert.Equal(reflectionInstances.Count, sourceGenInstances.Count);

        // Both should have all decorated
        Assert.All(reflectionInstances, i => Assert.IsType<MultiRegDecorator>(i));
        Assert.All(sourceGenInstances, i => Assert.IsType<MultiRegDecorator>(i));

        // Both should produce same set of values
        var reflectionValues = reflectionInstances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        var sourceGenValues = sourceGenInstances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Equal(reflectionValues, sourceGenValues);
    }

    [Fact]
    public void ChainedDecorators_WithMultipleRegistrations_AllDecorated_Parity()
    {
        // Arrange: Two decorators applied to two registrations
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddSingleton<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .AddDecorator<IMultiRegTestService, MultiRegSecondDecorator>()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddSingleton<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .AddDecorator<IMultiRegTestService, MultiRegSecondDecorator>()
            .BuildServiceProvider();

        // Act
        var reflectionInstances = reflectionProvider.GetServices<IMultiRegTestService>().ToList();
        var sourceGenInstances = sourceGenProvider.GetServices<IMultiRegTestService>().ToList();

        // Assert
        Assert.Equal(2, reflectionInstances.Count);
        Assert.Equal(2, sourceGenInstances.Count);

        // All should be wrapped by the outer decorator
        Assert.All(reflectionInstances, i => Assert.IsType<MultiRegSecondDecorator>(i));
        Assert.All(sourceGenInstances, i => Assert.IsType<MultiRegSecondDecorator>(i));

        // Verify complete chain
        var reflectionValues = reflectionInstances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        var sourceGenValues = sourceGenInstances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        
        Assert.Contains("Second(Decorated: ServiceA)", reflectionValues);
        Assert.Contains("Second(Decorated: ServiceB)", reflectionValues);
        Assert.Equal(reflectionValues, sourceGenValues);
    }

    [Fact]
    public void Decorator_WithDifferentLifetimes_PreservesLifetimes_Parity()
    {
        // Arrange: Different lifetimes for different implementations
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddScoped<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegTestService, MultiRegServiceA>();
                services.AddScoped<IMultiRegTestService, MultiRegServiceB>();
            })
            .AddDecorator<IMultiRegTestService, MultiRegDecorator>()
            .BuildServiceProvider();

        // Act - Use scope to verify lifetimes
        using var reflectionScope1 = reflectionProvider.CreateScope();
        using var reflectionScope2 = reflectionProvider.CreateScope();
        using var sourceGenScope1 = sourceGenProvider.CreateScope();
        using var sourceGenScope2 = sourceGenProvider.CreateScope();

        var reflectionInstances1 = reflectionScope1.ServiceProvider.GetServices<IMultiRegTestService>().ToList();
        var reflectionInstances2 = reflectionScope2.ServiceProvider.GetServices<IMultiRegTestService>().ToList();
        var sourceGenInstances1 = sourceGenScope1.ServiceProvider.GetServices<IMultiRegTestService>().ToList();
        var sourceGenInstances2 = sourceGenScope2.ServiceProvider.GetServices<IMultiRegTestService>().ToList();

        // Assert - Both should have 2 instances
        Assert.Equal(2, reflectionInstances1.Count);
        Assert.Equal(2, sourceGenInstances1.Count);

        // All should be decorated
        Assert.All(reflectionInstances1, i => Assert.IsType<MultiRegDecorator>(i));
        Assert.All(sourceGenInstances1, i => Assert.IsType<MultiRegDecorator>(i));
    }
}

public interface IMultiRegTestService
{
    string GetValue();
}

[DoNotAutoRegister]
public sealed class MultiRegServiceA : IMultiRegTestService
{
    public string GetValue() => "ServiceA";
}

[DoNotAutoRegister]
public sealed class MultiRegServiceB : IMultiRegTestService
{
    public string GetValue() => "ServiceB";
}

[DoNotAutoRegister]
public sealed class MultiRegDecorator : IMultiRegTestService
{
    private readonly IMultiRegTestService _inner;

    public MultiRegDecorator(IMultiRegTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Decorated: {_inner.GetValue()}";
}

[DoNotAutoRegister]
public sealed class MultiRegSecondDecorator : IMultiRegTestService
{
    private readonly IMultiRegTestService _inner;

    public MultiRegSecondDecorator(IMultiRegTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Second({_inner.GetValue()})";
}
