using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

public sealed class NeedlrSourceGenBootstrapRegisterPluginsTests : IDisposable
{
    public NeedlrSourceGenBootstrapRegisterPluginsTests()
    {
        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();
    }

    public void Dispose()
    {
        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();
    }

    [Fact]
    public void RegisterPlugins_MakesTypeDiscoverable_ViaTryGetProviders()
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(() =>
        [
            new PluginTypeInfo(
                typeof(CrossGeneratorPlugin),
                [typeof(ICrossGeneratorPlugin)],
                static () => new CrossGeneratorPlugin(),
                [])
        ]);

        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out var injectableProvider, out var pluginProvider);

        Assert.True(found);
        var plugins = pluginProvider!().ToList();
        Assert.Single(plugins, p => p.PluginType == typeof(CrossGeneratorPlugin));
    }

    [Fact]
    public void RegisterPlugins_AssemblyFilter_WorksInGeneratedPluginFactory()
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(() =>
        [
            new PluginTypeInfo(
                typeof(CrossGeneratorPlugin),
                [typeof(ICrossGeneratorPlugin)],
                static () => new CrossGeneratorPlugin(),
                [])
        ]);

        var _ = NeedlrSourceGenBootstrap.TryGetProviders(
            out var __, out var pluginProvider);
        var factory = new GeneratedPluginFactory(pluginProvider!);

        var pluginsInAssembly = factory.CreatePluginsFromAssemblies<ICrossGeneratorPlugin>(
            [typeof(CrossGeneratorPlugin).Assembly]).ToList();

        Assert.Single(pluginsInAssembly);
        Assert.IsType<CrossGeneratorPlugin>(pluginsInAssembly[0]);
    }

    [Fact]
    public void RegisterPlugins_AssemblyFilter_ExcludesTypesFromOtherAssemblies()
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(() =>
        [
            new PluginTypeInfo(
                typeof(CrossGeneratorPlugin),
                [typeof(ICrossGeneratorPlugin)],
                static () => new CrossGeneratorPlugin(),
                [])
        ]);

        var _ = NeedlrSourceGenBootstrap.TryGetProviders(
            out var __, out var pluginProvider);
        var factory = new GeneratedPluginFactory(pluginProvider!);

        var pluginsInOtherAssembly = factory.CreatePluginsFromAssemblies<ICrossGeneratorPlugin>(
            [typeof(string).Assembly]).ToList();

        Assert.Empty(pluginsInOtherAssembly);
    }

    [Fact]
    public void RegisterPlugins_CalledTwiceWithSameType_DeduplicatesMergedResult()
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(() =>
        [
            new PluginTypeInfo(
                typeof(CrossGeneratorPlugin),
                [typeof(ICrossGeneratorPlugin)],
                static () => new CrossGeneratorPlugin(),
                [])
        ]);

        NeedlrSourceGenBootstrap.RegisterPlugins(() =>
        [
            new PluginTypeInfo(
                typeof(CrossGeneratorPlugin),
                [typeof(ICrossGeneratorPlugin)],
                static () => new CrossGeneratorPlugin(),
                [])
        ]);

        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _, out var pluginProvider);

        Assert.True(found);
        var plugins = pluginProvider!().ToList();
        Assert.Single(plugins, p => p.PluginType == typeof(CrossGeneratorPlugin));
    }

    [Fact]
    public void RegisterPlugins_CombinedWithRegister_MergesAllPluginsWithDeduplication()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [],
            () =>
            [
                new PluginTypeInfo(
                    typeof(CrossGeneratorPlugin),
                    [typeof(ICrossGeneratorPlugin)],
                    static () => new CrossGeneratorPlugin(),
                    []),
                new PluginTypeInfo(
                    typeof(AnotherPlugin),
                    [typeof(ICrossGeneratorPlugin)],
                    static () => new AnotherPlugin(),
                    [])
            ]);

        NeedlrSourceGenBootstrap.RegisterPlugins(() =>
        [
            new PluginTypeInfo(
                typeof(CrossGeneratorPlugin),
                [typeof(ICrossGeneratorPlugin)],
                static () => new CrossGeneratorPlugin(),
                [])
        ]);

        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _, out var pluginProvider);

        Assert.True(found);
        var plugins = pluginProvider!().ToList();
        Assert.Equal(2, plugins.Count);
        Assert.Contains(plugins, p => p.PluginType == typeof(CrossGeneratorPlugin));
        Assert.Contains(plugins, p => p.PluginType == typeof(AnotherPlugin));
    }

    [Fact]
    public void RegisterPlugins_NullProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NeedlrSourceGenBootstrap.RegisterPlugins(null!));
    }

    private interface ICrossGeneratorPlugin { }

    private sealed class CrossGeneratorPlugin : ICrossGeneratorPlugin { }

    private sealed class AnotherPlugin : ICrossGeneratorPlugin { }
}
