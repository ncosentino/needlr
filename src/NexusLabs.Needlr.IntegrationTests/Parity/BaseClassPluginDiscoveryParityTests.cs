using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests to verify that plugin discovery via base class inheritance
/// works identically in both reflection and source-gen modes.
/// </summary>
public sealed class BaseClassPluginDiscoveryParityTests
{
    private static readonly Assembly[] TestAssemblies = [typeof(PluginConfigurationBase).Assembly];

    [Fact]
    public void Reflection_DiscoverPlugins_ByBaseClass_FindsAllImplementations()
    {
        // Arrange
        var factory = new ReflectionPluginFactory();

        // Act
        var plugins = factory.CreatePluginsFromAssemblies<PluginConfigurationBase>(TestAssemblies).ToList();

        // Assert - should find ConfigA, ConfigB, and ConfigC
        Assert.Equal(3, plugins.Count);
        Assert.Contains(plugins, p => p.Name == "ConfigA");
        Assert.Contains(plugins, p => p.Name == "ConfigB");
        Assert.Contains(plugins, p => p.Name == "ConfigC");
    }

    [Fact]
    public void SourceGen_DiscoverPlugins_ByBaseClass_FindsAllImplementations()
    {
        // Arrange
        var factory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var plugins = factory.CreatePluginsFromAssemblies<PluginConfigurationBase>(TestAssemblies).ToList();

        // Assert - should find ConfigA, ConfigB, and ConfigC
        Assert.Equal(3, plugins.Count);
        Assert.Contains(plugins, p => p.Name == "ConfigA");
        Assert.Contains(plugins, p => p.Name == "ConfigB");
        Assert.Contains(plugins, p => p.Name == "ConfigC");
    }

    [Fact]
    public void Parity_DiscoverPlugins_ByBaseClass_BothFindSamePlugins()
    {
        // Arrange
        var reflectionFactory = new ReflectionPluginFactory();
        var sourceGenFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<PluginConfigurationBase>(TestAssemblies)
            .OrderBy(p => p.Name)
            .ToList();
        var sourceGenPlugins = sourceGenFactory
            .CreatePluginsFromAssemblies<PluginConfigurationBase>(TestAssemblies)
            .OrderBy(p => p.Name)
            .ToList();

        // Assert - same count
        Assert.Equal(reflectionPlugins.Count, sourceGenPlugins.Count);

        // Assert - same plugin names
        for (int i = 0; i < reflectionPlugins.Count; i++)
        {
            Assert.Equal(reflectionPlugins[i].Name, sourceGenPlugins[i].Name);
        }
    }

    [Fact]
    public void Reflection_DiscoverPlugins_ByIntermediateBaseClass_FindsOnlyDerived()
    {
        // Arrange
        var factory = new ReflectionPluginFactory();

        // Act - query by the intermediate base class
        var plugins = factory
            .CreatePluginsFromAssemblies<SpecializedPluginConfigurationBase>(TestAssemblies)
            .ToList();

        // Assert - should only find ConfigC (the one that inherits from SpecializedPluginConfigurationBase)
        Assert.Single(plugins);
        Assert.Equal("ConfigC", plugins[0].Name);
    }

    [Fact]
    public void SourceGen_DiscoverPlugins_ByIntermediateBaseClass_FindsOnlyDerived()
    {
        // Arrange
        var factory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act - query by the intermediate base class
        var plugins = factory
            .CreatePluginsFromAssemblies<SpecializedPluginConfigurationBase>(TestAssemblies)
            .ToList();

        // Assert - should only find ConfigC (the one that inherits from SpecializedPluginConfigurationBase)
        Assert.Single(plugins);
        Assert.Equal("ConfigC", plugins[0].Name);
    }

    [Fact]
    public void Parity_DiscoverPlugins_ByIntermediateBaseClass_BothFindSame()
    {
        // Arrange
        var reflectionFactory = new ReflectionPluginFactory();
        var sourceGenFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<SpecializedPluginConfigurationBase>(TestAssemblies)
            .ToList();
        var sourceGenPlugins = sourceGenFactory
            .CreatePluginsFromAssemblies<SpecializedPluginConfigurationBase>(TestAssemblies)
            .ToList();

        // Assert
        Assert.Equal(reflectionPlugins.Count, sourceGenPlugins.Count);
        Assert.Equal(
            reflectionPlugins.Select(p => p.Name).OrderBy(x => x),
            sourceGenPlugins.Select(p => p.Name).OrderBy(x => x));
    }
}
