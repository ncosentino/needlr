using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Tests that verify Needlr prevents duplicate plugin execution.
/// A plugin type should never be executed more than once, even if the
/// plugin factory returns it multiple times.
/// </summary>
public sealed class DuplicatePluginPreventionTests
{
    /// <summary>
    /// Test plugin that tracks how many times Configure is called.
    /// </summary>
    public sealed class CountingWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
    {
        public static int ConfigureCallCount { get; private set; }

        public static void Reset() => ConfigureCallCount = 0;

        public void Configure(WebApplicationBuilderPluginOptions options)
        {
            ConfigureCallCount++;
        }
    }

    /// <summary>
    /// Test plugin that tracks how many times Configure is called.
    /// </summary>
    public sealed class CountingWebApplicationPlugin : IWebApplicationPlugin
    {
        public static int ConfigureCallCount { get; private set; }

        public static void Reset() => ConfigureCallCount = 0;

        public void Configure(WebApplicationPluginOptions options)
        {
            ConfigureCallCount++;
        }
    }

    /// <summary>
    /// Test plugin that tracks how many times Configure is called.
    /// </summary>
    public sealed class CountingServiceCollectionPlugin : IServiceCollectionPlugin
    {
        public static int ConfigureCallCount { get; private set; }

        public static void Reset() => ConfigureCallCount = 0;

        public void Configure(ServiceCollectionPluginOptions options)
        {
            ConfigureCallCount++;
        }
    }

    /// <summary>
    /// A plugin factory that intentionally returns duplicate plugin instances.
    /// This simulates a bug where the same plugin type is discovered twice.
    /// </summary>
    private sealed class DuplicatingPluginFactory : IPluginFactory
    {
        private readonly int _duplicateCount;

        public DuplicatingPluginFactory(int duplicateCount = 2)
        {
            _duplicateCount = duplicateCount;
        }

        public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin>(
            IEnumerable<Assembly> assemblies) where TPlugin : class
        {
            // Return duplicates of each plugin type
            if (typeof(TPlugin) == typeof(IWebApplicationBuilderPlugin))
            {
                for (int i = 0; i < _duplicateCount; i++)
                {
                    yield return (TPlugin)(object)new CountingWebApplicationBuilderPlugin();
                }
            }
            else if (typeof(TPlugin) == typeof(IWebApplicationPlugin))
            {
                for (int i = 0; i < _duplicateCount; i++)
                {
                    yield return (TPlugin)(object)new CountingWebApplicationPlugin();
                }
            }
            else if (typeof(TPlugin) == typeof(IServiceCollectionPlugin))
            {
                for (int i = 0; i < _duplicateCount; i++)
                {
                    yield return (TPlugin)(object)new CountingServiceCollectionPlugin();
                }
            }
        }

        public IEnumerable<object> CreatePluginsWithAttributeFromAssemblies<TAttribute>(
            IEnumerable<Assembly> assemblies) where TAttribute : Attribute
        {
            yield break;
        }

        public IEnumerable<TPlugin> CreatePluginsFromAssemblies<TPlugin, TAttribute>(
            IEnumerable<Assembly> assemblies)
            where TPlugin : class
            where TAttribute : Attribute
        {
            yield break;
        }
    }

    /// <summary>
    /// Verifies that when a plugin factory returns duplicate plugin instances,
    /// the WebApplicationFactory only executes each plugin type once.
    /// </summary>
    [Fact]
    public void WebApplicationFactory_WithDuplicateBuilderPlugins_ExecutesEachPluginTypeOnlyOnce()
    {
        // Arrange
        CountingWebApplicationBuilderPlugin.Reset();
        CountingWebApplicationPlugin.Reset();

        var duplicatingFactory = new DuplicatingPluginFactory(duplicateCount: 3);
        var mockServiceProviderBuilder = new Mock<IServiceProviderBuilder>();
        mockServiceProviderBuilder
            .Setup(x => x.GetCandidateAssemblies())
            .Returns(Array.Empty<Assembly>());

        var mockServiceCollectionPopulator = new Mock<IServiceCollectionPopulator>();
        mockServiceCollectionPopulator
            .Setup(x => x.RegisterToServiceCollection(
                It.IsAny<IServiceCollection>(),
                It.IsAny<Microsoft.Extensions.Configuration.IConfiguration>(),
                It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns<IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration, IReadOnlyList<Assembly>>(
                (services, config, assemblies) => services);

        var webAppFactory = new WebApplicationFactory(
            mockServiceProviderBuilder.Object,
            mockServiceCollectionPopulator.Object,
            duplicatingFactory);

        var createOptions = new CreateWebApplicationOptions(
            new WebApplicationOptions(),
            new Mock<ILogger>().Object);

        // Act
        var app = webAppFactory.Create(
            createOptions,
            () => WebApplication.CreateBuilder());

        // Assert - Each plugin type should only be executed once, not 3 times
        Assert.Equal(1, CountingWebApplicationBuilderPlugin.ConfigureCallCount);
        Assert.Equal(1, CountingWebApplicationPlugin.ConfigureCallCount);
    }

    /// <summary>
    /// Verifies that the same plugin type is not executed twice even when
    /// discovered from multiple sources (e.g., multiple assemblies registering the same type).
    /// </summary>
    [Fact]
    public void WebApplicationFactory_WithSamePluginFromMultipleSources_ExecutesOnlyOnce()
    {
        // Arrange
        CountingWebApplicationBuilderPlugin.Reset();

        // Create a factory that returns the same plugin type twice (simulating discovery from 2 assemblies)
        var duplicatingFactory = new DuplicatingPluginFactory(duplicateCount: 2);
        var mockServiceProviderBuilder = new Mock<IServiceProviderBuilder>();
        mockServiceProviderBuilder
            .Setup(x => x.GetCandidateAssemblies())
            .Returns(Array.Empty<Assembly>());

        var mockServiceCollectionPopulator = new Mock<IServiceCollectionPopulator>();
        mockServiceCollectionPopulator
            .Setup(x => x.RegisterToServiceCollection(
                It.IsAny<IServiceCollection>(),
                It.IsAny<Microsoft.Extensions.Configuration.IConfiguration>(),
                It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns<IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration, IReadOnlyList<Assembly>>(
                (services, config, assemblies) => services);

        var webAppFactory = new WebApplicationFactory(
            mockServiceProviderBuilder.Object,
            mockServiceCollectionPopulator.Object,
            duplicatingFactory);

        var createOptions = new CreateWebApplicationOptions(
            new WebApplicationOptions(),
            new Mock<ILogger>().Object);

        // Act
        var app = webAppFactory.Create(
            createOptions,
            () => WebApplication.CreateBuilder());

        // Assert - Plugin should only run once despite being returned twice
        Assert.Equal(1, CountingWebApplicationBuilderPlugin.ConfigureCallCount);
    }

    /// <summary>
    /// Verifies that different plugin types are all executed (no over-aggressive deduplication).
    /// </summary>
    [Fact]
    public void WebApplicationFactory_WithDifferentPluginTypes_ExecutesAll()
    {
        // Arrange
        CountingWebApplicationBuilderPlugin.Reset();
        CountingWebApplicationPlugin.Reset();

        // Factory that returns one of each type (no duplicates)
        var normalFactory = new DuplicatingPluginFactory(duplicateCount: 1);
        var mockServiceProviderBuilder = new Mock<IServiceProviderBuilder>();
        mockServiceProviderBuilder
            .Setup(x => x.GetCandidateAssemblies())
            .Returns(Array.Empty<Assembly>());

        var mockServiceCollectionPopulator = new Mock<IServiceCollectionPopulator>();
        mockServiceCollectionPopulator
            .Setup(x => x.RegisterToServiceCollection(
                It.IsAny<IServiceCollection>(),
                It.IsAny<Microsoft.Extensions.Configuration.IConfiguration>(),
                It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns<IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration, IReadOnlyList<Assembly>>(
                (services, config, assemblies) => services);

        var webAppFactory = new WebApplicationFactory(
            mockServiceProviderBuilder.Object,
            mockServiceCollectionPopulator.Object,
            normalFactory);

        var createOptions = new CreateWebApplicationOptions(
            new WebApplicationOptions(),
            new Mock<ILogger>().Object);

        // Act
        var app = webAppFactory.Create(
            createOptions,
            () => WebApplication.CreateBuilder());

        // Assert - Both different plugin types should execute
        Assert.Equal(1, CountingWebApplicationBuilderPlugin.ConfigureCallCount);
        Assert.Equal(1, CountingWebApplicationPlugin.ConfigureCallCount);
    }
}
