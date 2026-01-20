using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Tests that verify IPluginFactory is accessible through plugin options
/// for both reflection and source-generation scenarios.
/// </summary>
public sealed class PluginOptionsPluginFactoryTests
{
    [Fact]
    public void ServiceCollectionPluginOptions_PluginFactory_IsAccessible()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();

        // Act
        var options = new ServiceCollectionPluginOptions(
            services,
            config,
            assemblies,
            reflectionFactory);

        // Assert
        Assert.NotNull(options.PluginFactory);
        Assert.Same(reflectionFactory, options.PluginFactory);
    }

    [Fact]
    public void ServiceCollectionPluginOptions_PluginFactory_CanDiscoverPlugins()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();

        var options = new ServiceCollectionPluginOptions(
            services,
            config,
            assemblies,
            reflectionFactory);

        // Act
        var plugins = options.PluginFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(options.Assemblies)
            .ToList();

        // Assert
        Assert.NotEmpty(plugins);
    }

    [Fact]
    public void ServiceCollectionPluginOptions_PluginFactory_WorksWithGeneratedFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var options = new ServiceCollectionPluginOptions(
            services,
            config,
            assemblies,
            generatedFactory);

        // Act
        var plugins = options.PluginFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(options.Assemblies)
            .ToList();

        // Assert
        Assert.NotEmpty(plugins);
        Assert.All(plugins, p => Assert.NotNull(p));
    }

    [Fact]
    public void PostBuildServiceCollectionPluginOptions_PluginFactory_IsAccessible()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var config = new ConfigurationBuilder().Build();
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();

        // Act
        var options = new PostBuildServiceCollectionPluginOptions(
            provider,
            config,
            assemblies,
            reflectionFactory);

        // Assert
        Assert.NotNull(options.PluginFactory);
        Assert.Same(reflectionFactory, options.PluginFactory);
    }

    [Fact]
    public void PostBuildServiceCollectionPluginOptions_PluginFactory_CanDiscoverPlugins()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var config = new ConfigurationBuilder().Build();
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var options = new PostBuildServiceCollectionPluginOptions(
            provider,
            config,
            assemblies,
            generatedFactory);

        // Act
        var plugins = options.PluginFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(options.Assemblies)
            .ToList();

        // Assert
        Assert.NotEmpty(plugins);
    }

    [Fact]
    public void PluginFactory_Parity_BothFactoriesReturnSamePluginsViaOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionOptions = new ServiceCollectionPluginOptions(
            services,
            config,
            assemblies,
            reflectionFactory);

        var generatedOptions = new ServiceCollectionPluginOptions(
            services,
            config,
            assemblies,
            generatedFactory);

        // Act
        var reflectionPlugins = reflectionOptions.PluginFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(reflectionOptions.Assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedOptions.PluginFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(generatedOptions.Assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        // Assert
        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void PluginFactory_NestedPluginDiscovery_WorksViaOptions()
    {
        // Arrange - simulates a plugin discovering other plugins
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new ReflectionPluginFactory();

        var options = new ServiceCollectionPluginOptions(
            services,
            config,
            assemblies,
            reflectionFactory);

        // Act - simulate nested plugin discovery (like the user's scenario)
        var outerPlugins = options.PluginFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(options.Assemblies)
            .ToList();

        // Each outer plugin could use the same factory to discover inner plugins
        var nestedPlugins = options.PluginFactory
            .CreatePluginsFromAssemblies<ITestPluginWithOutput>(options.Assemblies)
            .ToList();

        // Assert
        Assert.NotEmpty(outerPlugins);
        Assert.NotEmpty(nestedPlugins);
    }
}
