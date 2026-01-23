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
}
