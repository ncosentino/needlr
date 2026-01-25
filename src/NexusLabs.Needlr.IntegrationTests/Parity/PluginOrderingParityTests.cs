using System.Reflection;

using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Tests that verify plugin ordering works identically between reflection and source-generated paths.
/// Test plugin types are defined in TestClasses.cs: IOrderedTestPlugin, FirstOrderPlugin, SecondOrderPlugin,
/// DefaultOrderPlugin, AnotherDefaultOrderPlugin, LaterOrderPlugin, LastOrderPlugin.
/// </summary>
public sealed class PluginOrderingParityTests
{
    [Fact]
    public void PluginOrder_NoAttribute_DefaultsToZero_Parity()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name)
            .ToList();

        // Assert - Both should return plugins in the same order
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginOrder_WithAttribute_SameOrderInBothPaths()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionOrder = reflectionFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name)
            .ToList();

        var generatedOrder = generatedFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name)
            .ToList();

        // Assert - Order should be identical between both paths
        Assert.Equal(reflectionOrder, generatedOrder);

        // Also verify the expected order (by [PluginOrder] values)
        Assert.Equal(nameof(FirstOrderPlugin), reflectionOrder[0]);   // Order: -100
        Assert.Equal(nameof(SecondOrderPlugin), reflectionOrder[1]);  // Order: -50
        // Order 0 plugins are sorted alphabetically
        Assert.Equal(nameof(AnotherDefaultOrderPlugin), reflectionOrder[2]); // Order: 0 (A before D)
        Assert.Equal(nameof(DefaultOrderPlugin), reflectionOrder[3]); // Order: 0
        Assert.Equal(nameof(LaterOrderPlugin), reflectionOrder[4]);   // Order: 50
        Assert.Equal(nameof(LastOrderPlugin), reflectionOrder[5]);    // Order: 100
    }

    [Fact]
    public void PluginOrder_SameOrder_SortedByTypeName_Parity()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .ToList();

        // Assert - Get the two default-order plugins (Order=0)
        var reflectionDefaultOrder = reflectionPlugins
            .Where(p => p.Name is nameof(DefaultOrderPlugin) or nameof(AnotherDefaultOrderPlugin))
            .Select(p => p.Name)
            .ToList();

        var generatedDefaultOrder = generatedPlugins
            .Where(p => p.Name is nameof(DefaultOrderPlugin) or nameof(AnotherDefaultOrderPlugin))
            .Select(p => p.Name)
            .ToList();

        // Both should be sorted alphabetically (AnotherDefaultOrderPlugin before DefaultOrderPlugin)
        Assert.Equal(reflectionDefaultOrder, generatedDefaultOrder);
        Assert.Equal(nameof(AnotherDefaultOrderPlugin), reflectionDefaultOrder[0]);
        Assert.Equal(nameof(DefaultOrderPlugin), reflectionDefaultOrder[1]);
    }

    [Fact]
    public void PluginOrder_NegativeBeforePositive_Parity()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .ToList();

        // Assert - First plugin should have negative order, last should have positive
        var firstReflection = reflectionPlugins.First();
        var lastReflection = reflectionPlugins.Last();
        var firstGenerated = generatedPlugins.First();
        var lastGenerated = generatedPlugins.Last();

        Assert.Equal(nameof(FirstOrderPlugin), firstReflection.Name);
        Assert.Equal(nameof(FirstOrderPlugin), firstGenerated.Name);
        Assert.Equal(nameof(LastOrderPlugin), lastReflection.Name);
        Assert.Equal(nameof(LastOrderPlugin), lastGenerated.Name);
    }

    [Fact]
    public void PluginOrder_DeterministicAcrossRuns_Parity()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act - Run multiple times
        var run1 = reflectionFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name).ToList();
        var run2 = reflectionFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name).ToList();
        var run3 = generatedFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name).ToList();
        var run4 = generatedFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .Select(p => p.Name).ToList();

        // Assert - All runs should produce identical order
        Assert.Equal(run1, run2);
        Assert.Equal(run2, run3);
        Assert.Equal(run3, run4);
    }

    [Fact]
    public void PluginOrder_AllPluginsDiscovered_Parity()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IOrderedTestPlugin>(assemblies)
            .ToList();

        // Assert - All 6 plugins should be discovered
        Assert.Equal(6, reflectionPlugins.Count);
        Assert.Equal(6, generatedPlugins.Count);

        // All expected plugins are present
        var expectedNames = new[]
        {
            nameof(FirstOrderPlugin),
            nameof(SecondOrderPlugin),
            nameof(AnotherDefaultOrderPlugin),
            nameof(DefaultOrderPlugin),
            nameof(LaterOrderPlugin),
            nameof(LastOrderPlugin)
        };

        var reflectionNames = reflectionPlugins.Select(p => p.Name).ToHashSet();
        var generatedNames = generatedPlugins.Select(p => p.Name).ToHashSet();

        foreach (var expectedName in expectedNames)
        {
            Assert.Contains(expectedName, reflectionNames);
            Assert.Contains(expectedName, generatedNames);
        }
    }

    [Fact]
    public void PluginOrder_PluginTypeInfoHasCorrectOrder()
    {
        // Arrange & Act
        var pluginTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes();
        
        // Assert - Find our test plugins and verify their Order values
        var orderedPlugins = pluginTypes
            .Where(p => typeof(IOrderedTestPlugin).IsAssignableFrom(p.PluginType))
            .OrderBy(p => p.Order)
            .ThenBy(p => p.PluginType.FullName)
            .ToList();

        Assert.Equal(6, orderedPlugins.Count);

        var firstPlugin = orderedPlugins.First(p => p.PluginType == typeof(FirstOrderPlugin));
        var secondPlugin = orderedPlugins.First(p => p.PluginType == typeof(SecondOrderPlugin));
        var defaultPlugin = orderedPlugins.First(p => p.PluginType == typeof(DefaultOrderPlugin));
        var anotherDefaultPlugin = orderedPlugins.First(p => p.PluginType == typeof(AnotherDefaultOrderPlugin));
        var laterPlugin = orderedPlugins.First(p => p.PluginType == typeof(LaterOrderPlugin));
        var lastPlugin = orderedPlugins.First(p => p.PluginType == typeof(LastOrderPlugin));

        Assert.Equal(-100, firstPlugin.Order);
        Assert.Equal(-50, secondPlugin.Order);
        Assert.Equal(0, defaultPlugin.Order);
        Assert.Equal(0, anotherDefaultPlugin.Order);
        Assert.Equal(50, laterPlugin.Order);
        Assert.Equal(100, lastPlugin.Order);
    }
}
