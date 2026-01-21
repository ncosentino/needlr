using System.Reflection;

using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify hosting plugin discovery is consistent between
/// reflection-based and source-generated factories.
/// </summary>
public sealed class HostingPluginParityTests
{
    [Fact]
    public void PluginParity_IHostApplicationBuilderPlugin_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IHostApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IHostApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginParity_IHostPlugin_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IHostPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IHostPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginParity_IHostApplicationBuilderPlugin_BothFactoriesInstantiateWorkingPlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IHostApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IHostApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginParity_IHostPlugin_BothFactoriesInstantiateWorkingPlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IHostPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IHostPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }
}
