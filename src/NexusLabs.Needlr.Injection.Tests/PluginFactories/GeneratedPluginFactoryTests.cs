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

    #region CreatePlugins (No Assembly Filter) Tests

    [Fact]
    public void CreatePlugins_ReturnsMatchingPlugins()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePlugins<ITestPlugin>().ToList();

        Assert.Single(plugins);
        Assert.IsType<TestPlugin>(plugins[0]);
    }

    [Fact]
    public void CreatePlugins_FiltersNonMatchingPluginTypes()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePlugins<IOtherPlugin>().ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePlugins_WithMultiplePlugins_ReturnsAll()
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

        var plugins = factory.CreatePlugins<ITestPlugin>().ToList();

        Assert.Equal(2, plugins.Count);
    }

    [Fact]
    public void CreatePlugins_WithEmptyProvider_ReturnsEmpty()
    {
        var factory = new GeneratedPluginFactory(() => []);

        var plugins = factory.CreatePlugins<ITestPlugin>().ToList();

        Assert.Empty(plugins);
    }

    #endregion

    #region CreatePluginsWithAttribute (No Assembly Filter) Tests

    [Fact]
    public void CreatePluginsWithAttribute_ReturnsPluginsWithAttribute()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            [typeof(TestAttribute)]);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePluginsWithAttribute<TestAttribute>().ToList();

        Assert.Single(plugins);
    }

    [Fact]
    public void CreatePluginsWithAttribute_FiltersPluginsWithoutAttribute()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []); // No attributes

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePluginsWithAttribute<TestAttribute>().ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsWithAttribute_WithMultiplePlugins_ReturnsOnlyAttributedOnes()
    {
        var plugin1 = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            [typeof(TestAttribute)]);
        var plugin2 = new PluginTypeInfo(
            typeof(AnotherTestPlugin),
            [typeof(ITestPlugin)],
            () => new AnotherTestPlugin(),
            []); // No attribute

        var factory = new GeneratedPluginFactory(() => [plugin1, plugin2]);

        var plugins = factory.CreatePluginsWithAttribute<TestAttribute>().ToList();

        Assert.Single(plugins);
    }

    #endregion

    #region CreatePlugins<TPlugin, TAttribute> (No Assembly Filter) Tests

    [Fact]
    public void CreatePlugins_WithTypeAndAttribute_ReturnsMatchingPlugins()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            [typeof(TestAttribute)]);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePlugins<ITestPlugin, TestAttribute>().ToList();

        Assert.Single(plugins);
    }

    [Fact]
    public void CreatePlugins_WithTypeAndAttribute_FiltersNonMatchingType()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            [typeof(TestAttribute)]);

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePlugins<IOtherPlugin, TestAttribute>().ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePlugins_WithTypeAndAttribute_FiltersNonMatchingAttribute()
    {
        var pluginInfo = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            []); // No attribute

        var factory = new GeneratedPluginFactory(() => [pluginInfo]);

        var plugins = factory.CreatePlugins<ITestPlugin, TestAttribute>().ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePlugins_WithTypeAndAttribute_RequiresBothToMatch()
    {
        var plugin1 = new PluginTypeInfo(
            typeof(TestPlugin),
            [typeof(ITestPlugin)],
            () => new TestPlugin(),
            [typeof(TestAttribute)]); // Has both
        var plugin2 = new PluginTypeInfo(
            typeof(AnotherTestPlugin),
            [typeof(ITestPlugin)],
            () => new AnotherTestPlugin(),
            []); // Has type but not attribute

        var factory = new GeneratedPluginFactory(() => [plugin1, plugin2]);

        var plugins = factory.CreatePlugins<ITestPlugin, TestAttribute>().ToList();

        Assert.Single(plugins);
        Assert.IsType<TestPlugin>(plugins[0]);
    }

    #endregion

    private interface ITestPlugin { }
    private interface IOtherPlugin { }

    [Test]
    private sealed class TestPlugin : ITestPlugin { }
    
    private sealed class AnotherTestPlugin : ITestPlugin { }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class TestAttribute : Attribute { }
}
