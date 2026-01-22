using System.Reflection;

using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

public sealed class PluginDiscoveryParityTests
{
    [Fact]
    public void PluginParity_ITestPlugin_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginParity_ITestPlugin_BothFactoriesInstantiateWorkingPlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        Assert.All(reflectionPlugins, p =>
        {
            Assert.NotNull(p);
            Assert.NotNull(p.Name);
        });

        Assert.All(generatedPlugins, p =>
        {
            Assert.NotNull(p);
            Assert.NotNull(p.Name);
        });

        var reflectionNames = reflectionPlugins.Select(p => p.Name).OrderBy(n => n).ToList();
        var generatedNames = generatedPlugins.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(reflectionNames, generatedNames);
    }

    [Fact]
    public void PluginParity_ITestPluginWithOutput_BothFactoriesDiscoverSamePlugins()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPluginWithOutput>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPluginWithOutput>(assemblies)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(MultiInterfaceTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(MultiInterfaceTestPlugin));

        foreach (var plugin in generatedPlugins)
        {
            Assert.NotNull(plugin.GetOutput());
        }
    }

    [Fact]
    public void PluginParity_PluginWithDependency_ExcludedByBothFactories()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(PluginWithDependency));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(PluginWithDependency));
    }

    [Fact]
    public void PluginParity_AbstractTestPlugin_ExcludedByBothFactories()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        Assert.DoesNotContain(reflectionPlugins, p => p.GetType() == typeof(AbstractTestPlugin));
        Assert.DoesNotContain(generatedPlugins, p => p.GetType() == typeof(AbstractTestPlugin));

        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(ConcreteTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(ConcreteTestPlugin));
    }

    [Fact]
    public void PluginParity_ManualRegistrationTestPlugin_DiscoveredByBothFactories()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        Assert.Contains(reflectionPlugins, p => p.GetType() == typeof(ManualRegistrationTestPlugin));
        Assert.Contains(generatedPlugins, p => p.GetType() == typeof(ManualRegistrationTestPlugin));
    }

    [Fact]
    public void PluginParity_EmptyAssemblies_BothFactoriesReturnEmpty()
    {
        var assemblies = Array.Empty<Assembly>();
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        Assert.Empty(reflectionPlugins);
        Assert.Empty(generatedPlugins);
    }

    [Fact]
    public void PluginParity_UnmatchedAssembly_BothFactoriesReturnEmpty()
    {
        var assemblies = new[] { typeof(object).Assembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        Assert.Empty(reflectionPlugins);
        Assert.Empty(generatedPlugins);
    }

    [Fact]
    public void PluginParity_GeneratedFactory_InstantiatesWithoutActivator()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var plugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        Assert.NotEmpty(plugins);
        foreach (var plugin in plugins)
        {
            Assert.NotNull(plugin);
            Assert.NotNull(plugin.Name);
            plugin.Execute();
        }
    }

    [Fact]
    public void PluginParity_PluginCount_IdenticalBetweenReflectionAndGenerated()
    {
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionCount = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Count();

        var generatedCount = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Count();

        Assert.Equal(reflectionCount, generatedCount);
        Assert.Equal(10, reflectionCount);
        Assert.Equal(10, generatedCount);
    }

    /// <summary>
    /// Verifies parity between reflection and source generation for Carter IWebApplicationBuilderPlugin.
    /// Both strategies should discover CarterWebApplicationBuilderPlugin from the Carter assembly.
    /// </summary>
    [Fact]
    public void PluginParity_Carter_IWebApplicationBuilderPlugin_BothFactoriesDiscoverSamePlugins()
    {
        // Arrange
        var carterAssembly = typeof(Carter.CarterWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { carterAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            Carter.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Contains("NexusLabs.Needlr.Carter.CarterWebApplicationBuilderPlugin", reflectionPlugins);
    }

    /// <summary>
    /// Verifies parity between reflection and source generation for Carter IWebApplicationPlugin.
    /// Both strategies should discover CarterWebApplicationPlugin from the Carter assembly.
    /// </summary>
    [Fact]
    public void PluginParity_Carter_IWebApplicationPlugin_BothFactoriesDiscoverSamePlugins()
    {
        // Arrange
        var carterAssembly = typeof(Carter.CarterWebApplicationPlugin).Assembly;
        var assemblies = new[] { carterAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            Carter.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Contains("NexusLabs.Needlr.Carter.CarterWebApplicationPlugin", reflectionPlugins);
    }

    /// <summary>
    /// Verifies parity between reflection and source generation for SignalR IWebApplicationBuilderPlugin.
    /// Both strategies should discover SignalRWebApplicationBuilderPlugin from the SignalR assembly.
    /// </summary>
    [Fact]
    public void PluginParity_SignalR_IWebApplicationBuilderPlugin_BothFactoriesDiscoverSamePlugins()
    {
        // Arrange
        var signalRAssembly = typeof(SignalR.SignalRWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { signalRAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            SignalR.Generated.TypeRegistry.GetPluginTypes);

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Contains("NexusLabs.Needlr.SignalR.SignalRWebApplicationBuilderPlugin", reflectionPlugins);
    }

    /// <summary>
    /// Verifies parity for all plugin types in the Carter assembly.
    /// Ensures reflection and source-gen discover identical sets of plugins.
    /// </summary>
    [Fact]
    public void PluginParity_Carter_AllPluginTypes_IdenticalBetweenReflectionAndGenerated()
    {
        // Arrange
        var carterAssembly = typeof(Carter.CarterWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { carterAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            Carter.Generated.TypeRegistry.GetPluginTypes);

        // Act - Get all IWebApplicationBuilderPlugin and IWebApplicationPlugin
        var reflectionBuilderPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();
        var reflectionAppPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        var generatedBuilderPlugins = generatedFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();
        var generatedAppPlugins = generatedFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        // Assert - Single plugin of each type discovered
        Assert.Single(reflectionBuilderPlugins);
        Assert.Single(generatedBuilderPlugins);
        Assert.Single(reflectionAppPlugins);
        Assert.Single(generatedAppPlugins);

        // Assert - Same types discovered
        Assert.Equal(reflectionBuilderPlugins.OrderBy(x => x), generatedBuilderPlugins.OrderBy(x => x));
        Assert.Equal(reflectionAppPlugins.OrderBy(x => x), generatedAppPlugins.OrderBy(x => x));
    }

    /// <summary>
    /// Verifies parity for all plugin types in the SignalR assembly.
    /// Ensures reflection and source-gen discover identical sets of plugins.
    /// </summary>
    [Fact]
    public void PluginParity_SignalR_AllPluginTypes_IdenticalBetweenReflectionAndGenerated()
    {
        // Arrange
        var signalRAssembly = typeof(SignalR.SignalRWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { signalRAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            SignalR.Generated.TypeRegistry.GetPluginTypes);

        // Act - Get IWebApplicationBuilderPlugin plugins
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<AspNet.IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        // Assert - Single plugin discovered (SignalR has 1 builder plugin)
        Assert.Single(reflectionPlugins);
        Assert.Single(generatedPlugins);

        // Assert - Same types discovered
        Assert.Equal(reflectionPlugins.OrderBy(x => x), generatedPlugins.OrderBy(x => x));
    }
}
