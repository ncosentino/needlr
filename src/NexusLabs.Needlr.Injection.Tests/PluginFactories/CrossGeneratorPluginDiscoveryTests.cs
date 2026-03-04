using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.PluginFactories;

/// <summary>
/// Evidence spike: proves that the mechanism behind RegisterPlugins() works correctly.
/// These tests use GeneratedPluginFactory directly to verify that types from two separate
/// "generator sources" can be combined and correctly discovered via assembly filtering.
/// </summary>
/// <remarks>
/// This simulates the multi-generator scenario:
/// - Source 1: TypeRegistryGenerator output (hand-written types it can see)
/// - Source 2: CacheProviderGenerator output (types invisible to TypeRegistryGenerator)
/// Both sets are combined in the merged provider that NeedlrSourceGenBootstrap.Combine() produces.
/// </remarks>
public sealed class CrossGeneratorPluginDiscoveryTests
{
    [Fact]
    public void CombinedProvider_FromTwoSources_DiscoversBothPluginTypes()
    {
        var source1 = new PluginTypeInfo(
            typeof(HandWrittenPlugin),
            [typeof(IEvidencePlugin)],
            () => new HandWrittenPlugin(),
            []);

        var source2 = new PluginTypeInfo(
            typeof(GeneratorEmittedPlugin),
            [typeof(IEvidencePlugin)],
            () => new GeneratorEmittedPlugin(),
            []);

        // Simulates what NeedlrSourceGenBootstrap.Combine() produces
        // when Register() is called by TypeRegistryGenerator and RegisterPlugins()
        // is called by CacheProviderGenerator.
        var factory = new GeneratedPluginFactory(() => [source1, source2]);

        var plugins = factory.CreatePluginsFromAssemblies<IEvidencePlugin>(
            [typeof(HandWrittenPlugin).Assembly]).ToList();

        Assert.Equal(2, plugins.Count);
        Assert.Contains(plugins, p => p is HandWrittenPlugin);
        Assert.Contains(plugins, p => p is GeneratorEmittedPlugin);
    }

    [Fact]
    public void CombinedProvider_AssemblyFilter_ExcludesTypesFromOtherAssemblies()
    {
        var source1 = new PluginTypeInfo(
            typeof(HandWrittenPlugin),
            [typeof(IEvidencePlugin)],
            () => new HandWrittenPlugin(),
            []);

        var source2 = new PluginTypeInfo(
            typeof(GeneratorEmittedPlugin),
            [typeof(IEvidencePlugin)],
            () => new GeneratorEmittedPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [source1, source2]);

        var plugins = factory.CreatePluginsFromAssemblies<IEvidencePlugin>(
            [typeof(string).Assembly]).ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void GeneratedPluginFactory_WithDuplicateType_ReturnsBothInstances()
    {
        // Documents WHERE deduplication lives: in NeedlrSourceGenBootstrap.Combine(),
        // not in GeneratedPluginFactory itself. The factory faithfully returns all entries
        // from its provider.
        var plugin1 = new PluginTypeInfo(
            typeof(HandWrittenPlugin),
            [typeof(IEvidencePlugin)],
            () => new HandWrittenPlugin(),
            []);

        var plugin2 = new PluginTypeInfo(
            typeof(HandWrittenPlugin),
            [typeof(IEvidencePlugin)],
            () => new HandWrittenPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [plugin1, plugin2]);

        var plugins = factory.CreatePluginsFromAssemblies<IEvidencePlugin>(
            [typeof(HandWrittenPlugin).Assembly]).ToList();

        Assert.Equal(2, plugins.Count);
    }

    private interface IEvidencePlugin { }

    private sealed class HandWrittenPlugin : IEvidencePlugin { }

    private sealed class GeneratorEmittedPlugin : IEvidencePlugin { }
}
