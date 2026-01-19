using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;
using NexusLabs.Needlr.Generators;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.PluginFactories;

public sealed class GeneratedPluginFactoryTests
{
    [Fact]
    public void CreatePluginsFromAssemblies_ReturnsMatchingPlugins()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePluginsFromAssemblies<ITestPlugin>(
            [typeof(TestPlugin).Assembly]).ToList();

        Assert.Single(plugins);
        Assert.IsType<TestPlugin>(plugins[0]);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_FiltersNonMatchingPlugins()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePluginsFromAssemblies<IOtherPlugin>(
            [typeof(TestPlugin).Assembly]).ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_EmptyAssembliesWithAllowAll_ReturnsAllPlugins()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [pluginInfo], allowAllWhenAssembliesEmpty: true);

        var plugins = factory.CreatePluginsFromAssemblies<ITestPlugin>([]).ToList();

        Assert.Single(plugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_EmptyAssembliesWithoutAllowAll_ReturnsEmpty()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [pluginInfo], allowAllWhenAssembliesEmpty: false);

        var plugins = factory.CreatePluginsFromAssemblies<ITestPlugin>([]).ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsWithAttributeFromAssemblies_ReturnsPluginsWithAttribute()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            [typeof(TestAttribute)]);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePluginsWithAttributeFromAssemblies<TestAttribute>(
            [typeof(TestPlugin).Assembly]).ToList();

        Assert.Single(plugins);
    }

    [Fact]
    public void CreatePluginsWithAttributeFromAssemblies_FiltersPluginsWithoutAttribute()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []); // No attributes

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePluginsWithAttributeFromAssemblies<TestAttribute>(
            [typeof(TestPlugin).Assembly]).ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithTypeAndAttribute_ReturnsMatchingPlugins()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            [typeof(TestAttribute)]);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePluginsFromAssemblies<ITestPlugin, TestAttribute>(
            [typeof(TestPlugin).Assembly]).ToList();

        Assert.Single(plugins);
    }

    [Fact]
    public void Constructor_NullPluginProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GeneratedPluginFactory(null!));
    }

    [Fact]
    public void CreatePluginsFromAssemblies_MultiplePlugins_ReturnsAll()
    {
        var plugin1 = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []);
        var plugin2 = new PluginTypeInfo(
            typeof(AnotherTestPlugin),
            [typeof(ITestPlugin)],
            () => new AnotherTestPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [plugin1, plugin2]);

        var plugins = factory.CreatePluginsFromAssemblies<ITestPlugin>(
            [typeof(TestPlugin).Assembly]).ToList();

        Assert.Equal(2, plugins.Count);
    }

    private interface ITestPlugin { }
    private interface IOtherPlugin { }

    [Test]
    private sealed class TestPlugin : ITestPlugin { }
    
    private sealed class AnotherTestPlugin : ITestPlugin { }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class TestAttribute : Attribute { }
}
