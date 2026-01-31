using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests for [DecoratorFor&lt;TService&gt;] attribute using source-generated discovery.
/// These tests verify that decorators marked with the attribute are correctly discovered
/// and applied at compile time via source generation.
/// </summary>
public sealed class DecoratorForAttributeSourceGenTests
{
    [Fact]
    public void DecoratorForAttribute_SingleDecorator_IsApplied()
    {
        // Arrange & Act
        // Use the source-gen path (no UsingReflection)
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Assert - The decorator should be applied
        var service = serviceProvider.GetRequiredService<IDecoratorForTestService>();
        Assert.NotNull(service);

        // The value should show decoration chain
        var value = service.GetValue();
        Assert.Contains("Original", value);
    }

    [Fact]
    public void DecoratorForAttribute_MultipleDecorators_AppliedInOrderByOrderProperty()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var service = serviceProvider.GetRequiredService<IDecoratorForTestService>();

        // Assert
        // With decorators at Order=0, Order=1, Order=2:
        // - Order=0 (Zero) wraps Original
        // - Order=1 (First) wraps Zero
        // - Order=2 (Second) wraps First
        // Result: Second(First(Zero(Original)))
        var value = service.GetValue();
        Assert.Equal("Second(First(Zero(Original)))", value);
    }

    [Fact]
    public void DecoratorForAttribute_ResolutionProducesCorrectChain()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var service = serviceProvider.GetRequiredService<IDecoratorForTestService>();

        // Assert
        var value = service.GetValue();

        // Zero should be innermost (directly wrapping Original)
        Assert.Contains("Zero(Original)", value);

        // First should wrap Zero
        Assert.Contains("First(Zero(", value);

        // Second should be outermost
        Assert.StartsWith("Second(", value);
    }

    [Fact]
    public void DecoratorForAttribute_DecoratorNotRegisteredAsInterface()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Assert - The decorator types should NOT be directly resolvable as the interface
        var allServices = serviceProvider.GetServices<IDecoratorForTestService>().ToList();

        // Should only have one registration for the interface (the decorated service)
        Assert.Single(allServices);

        // The single service should be the decorated version
        var service = allServices.First();
        Assert.Contains("Second(", service.GetValue());
    }

    [Fact]
    public void Decorator_WithMultipleRegistrations_AllDecorated_SourceGen()
    {
        // Arrange: Register two implementations via callback, decorator applied by source-gen AddDecorator call
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegSourceGenTestService, MultiRegSourceGenServiceA>();
                services.AddSingleton<IMultiRegSourceGenTestService, MultiRegSourceGenServiceB>();
            })
            .AddDecorator<IMultiRegSourceGenTestService, MultiRegSourceGenDecorator>()
            .BuildServiceProvider();

        // Act
        var instances = serviceProvider.GetServices<IMultiRegSourceGenTestService>().ToList();

        // Assert - Both registrations should be decorated
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<MultiRegSourceGenDecorator>(instance));

        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Decorated: SourceGenA", values);
        Assert.Contains("Decorated: SourceGenB", values);
    }

    [Fact]
    public void ChainedDecorators_WithMultipleRegistrations_AllDecorated_SourceGen()
    {
        // Arrange: Two decorators applied to two registrations
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegSourceGenTestService, MultiRegSourceGenServiceA>();
                services.AddSingleton<IMultiRegSourceGenTestService, MultiRegSourceGenServiceB>();
            })
            .AddDecorator<IMultiRegSourceGenTestService, MultiRegSourceGenDecorator>()
            .AddDecorator<IMultiRegSourceGenTestService, MultiRegSourceGenSecondDecorator>()
            .BuildServiceProvider();

        // Act
        var instances = serviceProvider.GetServices<IMultiRegSourceGenTestService>().ToList();

        // Assert
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<MultiRegSourceGenSecondDecorator>(instance));

        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Second(Decorated: SourceGenA)", values);
        Assert.Contains("Second(Decorated: SourceGenB)", values);
    }

    [Fact]
    public void Decorator_WithDifferentLifetimes_PreservesLifetimes_SourceGen()
    {
        // Arrange: Different lifetimes for different implementations
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddSingleton<IMultiRegSourceGenTestService, MultiRegSourceGenServiceA>();
                services.AddScoped<IMultiRegSourceGenTestService, MultiRegSourceGenServiceB>();
            })
            .AddDecorator<IMultiRegSourceGenTestService, MultiRegSourceGenDecorator>()
            .BuildServiceProvider();

        // Act - Use scope to verify lifetimes
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var instances1 = scope1.ServiceProvider.GetServices<IMultiRegSourceGenTestService>().ToList();
        var instances2 = scope2.ServiceProvider.GetServices<IMultiRegSourceGenTestService>().ToList();

        // Assert - Both should have 2 instances
        Assert.Equal(2, instances1.Count);
        Assert.Equal(2, instances2.Count);

        // All should be decorated
        Assert.All(instances1, i => Assert.IsType<MultiRegSourceGenDecorator>(i));
        Assert.All(instances2, i => Assert.IsType<MultiRegSourceGenDecorator>(i));
    }
}

public interface IMultiRegSourceGenTestService
{
    string GetValue();
}

[DoNotAutoRegister]
public sealed class MultiRegSourceGenServiceA : IMultiRegSourceGenTestService
{
    public string GetValue() => "SourceGenA";
}

[DoNotAutoRegister]
public sealed class MultiRegSourceGenServiceB : IMultiRegSourceGenTestService
{
    public string GetValue() => "SourceGenB";
}

[DoNotAutoRegister]
public sealed class MultiRegSourceGenDecorator : IMultiRegSourceGenTestService
{
    private readonly IMultiRegSourceGenTestService _inner;

    public MultiRegSourceGenDecorator(IMultiRegSourceGenTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Decorated: {_inner.GetValue()}";
}

[DoNotAutoRegister]
public sealed class MultiRegSourceGenSecondDecorator : IMultiRegSourceGenTestService
{
    private readonly IMultiRegSourceGenTestService _inner;

    public MultiRegSourceGenSecondDecorator(IMultiRegSourceGenTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Second({_inner.GetValue()})";
}
