using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify [DecoratorFor&lt;TService&gt;] attribute handling is consistent
/// between reflection and source-generation paths. These tests compare both paths
/// to ensure they produce equivalent results.
/// </summary>
public sealed class DecoratorForAttributeParityTests
{
    [Fact]
    public void DecoratorForAttribute_SingleDecorator_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetRequiredService<IDecoratorForTestService>();
        var sourceGenService = sourceGenProvider.GetRequiredService<IDecoratorForTestService>();

        // Assert - Both should produce the same result
        var reflectionValue = reflectionService.GetValue();
        var sourceGenValue = sourceGenService.GetValue();

        Assert.Equal(reflectionValue, sourceGenValue);
        Assert.Contains("Original", reflectionValue);
    }

    [Fact]
    public void DecoratorForAttribute_MultipleDecoratorsWithOrdering_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetRequiredService<IDecoratorForTestService>();
        var sourceGenService = sourceGenProvider.GetRequiredService<IDecoratorForTestService>();

        // Assert - Both should have the same decorator ordering
        var reflectionValue = reflectionService.GetValue();
        var sourceGenValue = sourceGenService.GetValue();

        // Expected: Second(First(Zero(Original)))
        Assert.Equal(reflectionValue, sourceGenValue);
        Assert.Equal("Second(First(Zero(Original)))", reflectionValue);
    }

    [Fact]
    public void DecoratorForAttribute_ServiceResolution_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var reflectionServices = reflectionProvider.GetServices<IDecoratorForTestService>().ToList();
        var sourceGenServices = sourceGenProvider.GetServices<IDecoratorForTestService>().ToList();

        // Assert - Both paths should have same number of service registrations
        Assert.Equal(reflectionServices.Count, sourceGenServices.Count);

        // Both should have exactly one decorated service
        Assert.Single(reflectionServices);
        Assert.Single(sourceGenServices);
    }

    [Fact]
    public void DecoratorForAttribute_DecoratorExclusion_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Resolve the decorator type directly as its concrete type
        var reflectionDecorators = reflectionProvider.GetServices<DecoratorForFirstDecorator>().ToList();
        var sourceGenDecorators = sourceGenProvider.GetServices<DecoratorForFirstDecorator>().ToList();

        // Assert - Decorators ARE registered as themselves (just not as the interface they decorate).
        // This is because they are valid injectable types with a constructor.
        // Both paths should register exactly 1 instance of the concrete decorator type.
        Assert.Single(reflectionDecorators);
        Assert.Single(sourceGenDecorators);
    }

    [Fact]
    public void DecoratorForAttribute_ChainIntegrity_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetRequiredService<IDecoratorForTestService>();
        var sourceGenService = sourceGenProvider.GetRequiredService<IDecoratorForTestService>();

        // Assert - Verify the complete chain structure matches
        var reflectionValue = reflectionService.GetValue();
        var sourceGenValue = sourceGenService.GetValue();

        // Both should have Zero as innermost decorator
        Assert.Contains("Zero(Original)", reflectionValue);
        Assert.Contains("Zero(Original)", sourceGenValue);

        // Both should have First wrapping Zero
        Assert.Contains("First(Zero(", reflectionValue);
        Assert.Contains("First(Zero(", sourceGenValue);

        // Both should have Second as outermost
        Assert.StartsWith("Second(", reflectionValue);
        Assert.StartsWith("Second(", sourceGenValue);
    }

    [Fact]
    public void DecoratorForAttribute_MultipleResolutions_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Resolve multiple times
        var reflectionResults = new List<string>();
        var sourceGenResults = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            reflectionResults.Add(reflectionProvider.GetRequiredService<IDecoratorForTestService>().GetValue());
            sourceGenResults.Add(sourceGenProvider.GetRequiredService<IDecoratorForTestService>().GetValue());
        }

        // Assert - All resolutions should produce consistent results
        Assert.All(reflectionResults, r => Assert.Equal(reflectionResults[0], r));
        Assert.All(sourceGenResults, r => Assert.Equal(sourceGenResults[0], r));

        // And both paths should produce the same result
        Assert.Equal(reflectionResults[0], sourceGenResults[0]);
    }
}
