using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Reflection;

/// <summary>
/// Integration tests for [DecoratorFor&lt;TService&gt;] attribute using reflection-based discovery.
/// These tests verify that decorators marked with the attribute are correctly discovered
/// and applied at runtime.
/// </summary>
public sealed class DecoratorForAttributeTests
{
    [Fact]
    public void DecoratorForAttribute_SingleDecorator_IsApplied()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Assert - The decorator should be applied
        var service = serviceProvider.GetRequiredService<IDecoratorForTestService>();
        Assert.NotNull(service);

        // The value should show decoration chain
        // With Order=0, Order=1, Order=2 decorators, chain is:
        // Second(First(Zero(Original)))
        var value = service.GetValue();
        Assert.Contains("Original", value);
    }

    [Fact]
    public void DecoratorForAttribute_MultipleDecorators_AppliedInOrderByOrderProperty()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingReflection()
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
    public void DecoratorForAttribute_Order0ThenOrder1_InnerIsOrder0()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var service = serviceProvider.GetRequiredService<IDecoratorForTestService>();

        // Assert
        // Verify the nesting order: outer decorators (higher order) wrap inner (lower order)
        var value = service.GetValue();

        // Zero should be innermost (directly wrapping Original)
        Assert.Contains("Zero(Original)", value);

        // First should wrap Zero
        Assert.Contains("First(Zero(", value);

        // Second should be outermost
        Assert.StartsWith("Second(", value);
    }

    [Fact]
    public void DecoratorForAttribute_WithManualServiceRegistration_DecoratorApplied()
    {
        // Arrange & Act
        // This test verifies that [DecoratorFor] attribute decorators work
        // with the automatically discovered base service
        var serviceProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var service = serviceProvider.GetRequiredService<IDecoratorForTestService>();

        // Assert - The decorators should all be applied
        var value = service.GetValue();
        Assert.Equal("Second(First(Zero(Original)))", value);
    }

    [Fact]
    public void DecoratorForAttribute_DecoratorNotRegisteredAsInterface()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Assert - The decorator types should NOT be directly resolvable as the interface
        // (they should only be applied via the decorator pattern)
        var allServices = serviceProvider.GetServices<IDecoratorForTestService>().ToList();

        // Should only have one registration for the interface (the decorated service)
        Assert.Single(allServices);

        // The single service should be the decorated version
        var service = allServices.First();
        Assert.Contains("Second(", service.GetValue());
    }
}
