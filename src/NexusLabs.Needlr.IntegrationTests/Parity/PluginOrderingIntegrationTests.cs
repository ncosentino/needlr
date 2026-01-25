using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Collection definition for plugin ordering tests.
/// Forces tests in this collection to run sequentially since they use static state.
/// </summary>
[CollectionDefinition("PluginOrderingTests", DisableParallelization = true)]
public sealed class PluginOrderingTestCollection;

/// <summary>
/// Integration tests that verify [PluginOrder] is respected when building service providers
/// through the Syringe API for both reflection and source-gen paths.
/// 
/// These tests use actual service provider construction to verify that IServiceCollectionPlugin
/// implementations are executed in the order specified by their [PluginOrder] attributes.
/// </summary>
[Collection("PluginOrderingTests")]
public sealed class PluginOrderingIntegrationTests : IDisposable
{
    /// <summary>
    /// Expected execution order for our test plugins:
    /// -100, -50, 0 (alphabetically: A before Z), 50, 100
    /// </summary>
    private static readonly string[] ExpectedOrder =
    [
        nameof(OrderMinus100Plugin),  // Order: -100
        nameof(OrderMinus50Plugin),   // Order: -50
        nameof(ADefaultOrderPlugin),  // Order: 0 (A < Z alphabetically)
        nameof(ZDefaultOrderPlugin),  // Order: 0 (Z > A alphabetically)
        nameof(Order50Plugin),        // Order: 50
        nameof(Order100Plugin),       // Order: 100
    ];

    /// <summary>
    /// Helper to get execution order as a List for IndexOf support.
    /// </summary>
    private static List<string> GetExecutionOrderList() => 
        PluginExecutionTracker.GetExecutionOrder().ToList();

    public PluginOrderingIntegrationTests()
    {
        // Reset before each test
        PluginExecutionTracker.Reset();
    }

    public void Dispose()
    {
        // Clean up after each test
        PluginExecutionTracker.Reset();
    }

    /// <summary>
    /// Verifies that plugins execute in the correct order when using reflection-based service provider.
    /// </summary>
    [Fact]
    public void ReflectionServiceProvider_ExecutesPluginsInOrder()
    {
        // Arrange & Act - Build service provider using reflection
        var serviceProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Assert - Verify execution order
        var executionOrder = PluginExecutionTracker.GetExecutionOrder();
        
        // Filter to only our test plugins (other plugins may also have executed)
        var orderedTestPlugins = executionOrder
            .Where(name => ExpectedOrder.Contains(name))
            .ToList();

        Assert.Equal(ExpectedOrder.Length, orderedTestPlugins.Count);
        Assert.Equal(ExpectedOrder, orderedTestPlugins);
    }

    /// <summary>
    /// Verifies that plugins execute in the correct order when using source-generated service provider.
    /// </summary>
    [Fact]
    public void SourceGenServiceProvider_ExecutesPluginsInOrder()
    {
        // Arrange & Act - Build service provider using source generation
        var serviceProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Assert - Verify execution order
        var executionOrder = PluginExecutionTracker.GetExecutionOrder();
        
        // Filter to only our test plugins (other plugins may also have executed)
        var orderedTestPlugins = executionOrder
            .Where(name => ExpectedOrder.Contains(name))
            .ToList();

        Assert.Equal(ExpectedOrder.Length, orderedTestPlugins.Count);
        Assert.Equal(ExpectedOrder, orderedTestPlugins);
    }

    /// <summary>
    /// Verifies that both reflection and source-gen paths execute plugins in the same order.
    /// </summary>
    [Fact]
    public void Parity_BothPathsExecutePluginsInSameOrder()
    {
        // Arrange - Build with reflection first
        PluginExecutionTracker.Reset();
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();
        var reflectionOrder = PluginExecutionTracker.GetExecutionOrder()
            .Where(name => ExpectedOrder.Contains(name))
            .ToList();

        // Build with source-gen
        PluginExecutionTracker.Reset();
        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
        var sourceGenOrder = PluginExecutionTracker.GetExecutionOrder()
            .Where(name => ExpectedOrder.Contains(name))
            .ToList();

        // Assert - Both paths should produce the same order
        Assert.Equal(reflectionOrder, sourceGenOrder);
        Assert.Equal(ExpectedOrder, reflectionOrder);
    }

    /// <summary>
    /// Verifies that negative order values execute before zero (default).
    /// </summary>
    [Fact]
    public void ReflectionServiceProvider_NegativeOrderExecutesBeforeDefault()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var executionOrder = GetExecutionOrderList();

        // Assert - Find the indices
        var minus100Index = executionOrder.IndexOf(nameof(OrderMinus100Plugin));
        var minus50Index = executionOrder.IndexOf(nameof(OrderMinus50Plugin));
        var defaultAIndex = executionOrder.IndexOf(nameof(ADefaultOrderPlugin));

        Assert.True(minus100Index < minus50Index, "-100 should execute before -50");
        Assert.True(minus50Index < defaultAIndex, "-50 should execute before 0");
    }

    /// <summary>
    /// Verifies that positive order values execute after zero (default).
    /// </summary>
    [Fact]
    public void SourceGenServiceProvider_PositiveOrderExecutesAfterDefault()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var executionOrder = GetExecutionOrderList();

        // Assert - Find the indices
        var defaultZIndex = executionOrder.IndexOf(nameof(ZDefaultOrderPlugin));
        var order50Index = executionOrder.IndexOf(nameof(Order50Plugin));
        var order100Index = executionOrder.IndexOf(nameof(Order100Plugin));

        Assert.True(defaultZIndex < order50Index, "0 should execute before 50");
        Assert.True(order50Index < order100Index, "50 should execute before 100");
    }

    /// <summary>
    /// Verifies that same-order plugins are sorted alphabetically by type name.
    /// </summary>
    [Fact]
    public void Parity_SameOrderPluginsSortedAlphabetically()
    {
        // Test reflection
        PluginExecutionTracker.Reset();
        new Syringe().UsingReflection().BuildServiceProvider();
        var reflectionOrder = GetExecutionOrderList();
        var reflectionAIndex = reflectionOrder.IndexOf(nameof(ADefaultOrderPlugin));
        var reflectionZIndex = reflectionOrder.IndexOf(nameof(ZDefaultOrderPlugin));

        // Test source-gen
        PluginExecutionTracker.Reset();
        new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
        var sourceGenOrder = GetExecutionOrderList();
        var sourceGenAIndex = sourceGenOrder.IndexOf(nameof(ADefaultOrderPlugin));
        var sourceGenZIndex = sourceGenOrder.IndexOf(nameof(ZDefaultOrderPlugin));

        // Assert - A should come before Z in both cases
        Assert.True(reflectionAIndex < reflectionZIndex, "Reflection: A should execute before Z");
        Assert.True(sourceGenAIndex < sourceGenZIndex, "SourceGen: A should execute before Z");
    }

    /// <summary>
    /// Verifies that plugin ordering is deterministic across multiple service provider builds.
    /// </summary>
    [Fact]
    public void Parity_PluginOrderingIsDeterministic()
    {
        // Build multiple times with reflection
        var reflectionOrders = new List<List<string>>();
        for (int i = 0; i < 3; i++)
        {
            PluginExecutionTracker.Reset();
            new Syringe().UsingReflection().BuildServiceProvider();
            reflectionOrders.Add(PluginExecutionTracker.GetExecutionOrder()
                .Where(name => ExpectedOrder.Contains(name))
                .ToList());
        }

        // Build multiple times with source-gen
        var sourceGenOrders = new List<List<string>>();
        for (int i = 0; i < 3; i++)
        {
            PluginExecutionTracker.Reset();
            new Syringe()
                .UsingGeneratedComponents(
                    NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                    NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
                .BuildServiceProvider();
            sourceGenOrders.Add(PluginExecutionTracker.GetExecutionOrder()
                .Where(name => ExpectedOrder.Contains(name))
                .ToList());
        }

        // Assert - All runs should produce identical order
        for (int i = 1; i < 3; i++)
        {
            Assert.Equal(reflectionOrders[0], reflectionOrders[i]);
            Assert.Equal(sourceGenOrders[0], sourceGenOrders[i]);
        }

        // Both paths should match
        Assert.Equal(reflectionOrders[0], sourceGenOrders[0]);
    }

    /// <summary>
    /// Verifies that all expected plugins are discovered and executed.
    /// </summary>
    [Fact]
    public void ReflectionServiceProvider_AllOrderedPluginsExecuted()
    {
        // Arrange & Act
        new Syringe().UsingReflection().BuildServiceProvider();
        var executionOrder = PluginExecutionTracker.GetExecutionOrder();

        // Assert - All expected plugins should have executed
        foreach (var expectedPlugin in ExpectedOrder)
        {
            Assert.Contains(expectedPlugin, executionOrder);
        }
    }

    /// <summary>
    /// Verifies that all expected plugins are discovered and executed.
    /// </summary>
    [Fact]
    public void SourceGenServiceProvider_AllOrderedPluginsExecuted()
    {
        // Arrange & Act
        new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
        var executionOrder = PluginExecutionTracker.GetExecutionOrder();

        // Assert - All expected plugins should have executed
        foreach (var expectedPlugin in ExpectedOrder)
        {
            Assert.Contains(expectedPlugin, executionOrder);
        }
    }
}
