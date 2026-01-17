using System.Reflection;

using NexusLabs.Needlr.Injection.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

public sealed class AttributeBasedPluginParityTests
{
    [Fact]
    public void AttributeParity_SpecialPluginAttribute_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Equal(3, reflectionPlugins.Count);
    }

    [Fact]
    public void AttributeParity_PriorityPluginAttribute_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, PriorityPluginAttribute>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, PriorityPluginAttribute>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Equal(2, reflectionPlugins.Count);
    }

    [Fact]
    public void AttributeParity_CreatePluginsWithAttribute_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsWithAttributeFromAssemblies<SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsWithAttributeFromAssemblies<SpecialPluginAttribute>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void AttributeParity_InheritedAttribute_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();

        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(InheritedAttributeTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(InheritedAttributeTestPlugin));
    }

    [Fact]
    public void AttributeParity_NoAttribute_ExcludedByBothFactories()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();

        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(NoAttributeTestPlugin));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(NoAttributeTestPlugin));
        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(SimpleTestPlugin));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(SimpleTestPlugin));
    }

    [Fact]
    public void AttributeParity_MultipleAttributes_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        var reflectionSpecialPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();
        var generatedSpecialPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, SpecialPluginAttribute>(assemblies)
            .ToList();

        var reflectionPriorityPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, PriorityPluginAttribute>(assemblies)
            .ToList();
        var generatedPriorityPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, PriorityPluginAttribute>(assemblies)
            .ToList();

        Assert.Contains(reflectionSpecialPlugins, p => p.GetType() == typeof(MultiAttributeTestPlugin));
        Assert.Contains(generatedSpecialPlugins, p => p.GetType() == typeof(MultiAttributeTestPlugin));
        Assert.Contains(reflectionPriorityPlugins, p => p.GetType() == typeof(MultiAttributeTestPlugin));
        Assert.Contains(generatedPriorityPlugins, p => p.GetType() == typeof(MultiAttributeTestPlugin));
    }

    [Fact]
    public void AttributeParity_NonExistentAttribute_BothFactoriesReturnEmpty()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin, ObsoleteAttribute>(assemblies)
            .ToList();
        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin, ObsoleteAttribute>(assemblies)
            .ToList();

        Assert.Empty(reflectionPlugins);
        Assert.Empty(generatedPlugins);
    }
}
