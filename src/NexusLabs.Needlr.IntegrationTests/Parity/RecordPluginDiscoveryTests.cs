using System.Reflection;

using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests proving that records ARE discoverable as plugins via IPluginFactory,
/// even though they are NOT auto-registered as services.
/// This is the CacheConfiguration use case.
/// </summary>
public sealed class RecordPluginDiscoveryTests
{
    private static readonly Assembly[] TestAssemblies = [typeof(PluginConfigurationRecord).Assembly];

    [Fact]
    public void Reflection_RecordPlugins_AreDiscoverable()
    {
        var factory = new ReflectionPluginFactory();

        var plugins = factory
            .CreatePluginsFromAssemblies<PluginConfigurationRecord>(TestAssemblies)
            .ToList();

        Assert.Equal(2, plugins.Count);
        Assert.Contains(plugins, p => p.Name == "RecordA");
        Assert.Contains(plugins, p => p.Name == "RecordB");
    }

    [Fact]
    public void SourceGen_RecordPlugins_AreDiscoverable()
    {
        var factory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var plugins = factory
            .CreatePluginsFromAssemblies<PluginConfigurationRecord>(TestAssemblies)
            .ToList();

        Assert.Equal(2, plugins.Count);
        Assert.Contains(plugins, p => p.Name == "RecordA");
        Assert.Contains(plugins, p => p.Name == "RecordB");
    }

    [Fact]
    public void Parity_RecordPluginDiscovery_BothFindSamePlugins()
    {
        var reflectionFactory = new ReflectionPluginFactory();
        var sourceGenFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<PluginConfigurationRecord>(TestAssemblies)
            .OrderBy(p => p.Name)
            .ToList();
        var sourceGenPlugins = sourceGenFactory
            .CreatePluginsFromAssemblies<PluginConfigurationRecord>(TestAssemblies)
            .OrderBy(p => p.Name)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, sourceGenPlugins.Count);
        for (int i = 0; i < reflectionPlugins.Count; i++)
        {
            Assert.Equal(reflectionPlugins[i].Name, sourceGenPlugins[i].Name);
        }
    }

    [Fact]
    public void Reflection_RecordWithRequiredMembers_IsDiscoverableAtRuntime()
    {
        // At runtime, Activator.CreateInstance can create records with required members
        // The 'required' modifier is only enforced at compile-time
        var factory = new ReflectionPluginFactory();

        var plugins = factory
            .CreatePluginsFromAssemblies<IRecordService>(TestAssemblies)
            .Where(p => p is RecordWithRequiredMembers)
            .ToList();

        // Reflection CAN discover and instantiate these at runtime
        Assert.Single(plugins);
    }

    [Fact]
    public void SourceGen_RecordWithRequiredMembers_NotDiscoverableAsPlugin()
    {
        // Source-gen excludes records with required members because the generated
        // code would fail to compile (cannot do `new RecordWithRequired()` without setting members)
        var factory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var plugins = factory
            .CreatePluginsFromAssemblies<IRecordService>(TestAssemblies)
            .Where(p => p is RecordWithRequiredMembers)
            .ToList();

        Assert.Empty(plugins);
    }
}
