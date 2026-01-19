using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;

using Xunit;

namespace NexusLabs.Needlr.Injection.Bundle.Tests;

public sealed class ServiceProviderBuilderTests
{
    [Fact]
    public void Constructor_ThrowsOnNullServiceCollectionPopulator()
    {
        // Arrange
        IServiceCollectionPopulator? populator = null;
        var assemblyProvider = new TestAssemblyProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ServiceProviderBuilder(populator!, assemblyProvider));
    }

    [Fact]
    public void Constructor_ThrowsOnNullAssemblyProvider()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        IAssemblyProvider? assemblyProvider = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ServiceProviderBuilder(populator, assemblyProvider!));
    }

    [Fact]
    public void Constructor_ThrowsOnNullAdditionalAssemblies()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ServiceProviderBuilder(populator, assemblyProvider, null!));
    }

    [Fact]
    public void GetCandidateAssemblies_ReturnsCombinedAssemblies()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var additionalAssemblies = new[] { typeof(ServiceProviderBuilderTests).Assembly };

        var builder = new ServiceProviderBuilder(populator, assemblyProvider, additionalAssemblies);

        // Act
        var assemblies = builder.GetCandidateAssemblies();

        // Assert
        Assert.NotNull(assemblies);
        Assert.Contains(typeof(ServiceProviderBuilderTests).Assembly, assemblies);
    }

    [Fact]
    public void GetCandidateAssemblies_DeduplicatesAssemblies()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        var thisAssembly = typeof(ServiceProviderBuilderTests).Assembly;
        var assemblyProvider = new TestAssemblyProvider(thisAssembly);
        var additionalAssemblies = new[] { thisAssembly }; // Same assembly

        var builder = new ServiceProviderBuilder(populator, assemblyProvider, additionalAssemblies);

        // Act
        var assemblies = builder.GetCandidateAssemblies();

        // Assert - should only appear once
        Assert.Equal(1, assemblies.Count(a => a == thisAssembly));
    }

    [Fact]
    public void Build_WithConfig_ReturnsServiceProvider()
    {
        // Arrange
        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [],
            () => []);

        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var builder = new ServiceProviderBuilder(populator, assemblyProvider);
        var config = new ConfigurationBuilder().Build();

        // Act
        var provider = builder.Build(config);

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Build_WithServicesAndConfig_ReturnsServiceProvider()
    {
        // Arrange
        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [],
            () => []);

        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var builder = new ServiceProviderBuilder(populator, assemblyProvider);
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        // Act
        var provider = builder.Build(services, config);

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Build_ThrowsOnNullServices()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var builder = new ServiceProviderBuilder(populator, assemblyProvider);
        var config = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, config));
    }

    [Fact]
    public void Build_ThrowsOnNullConfig()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var builder = new ServiceProviderBuilder(populator, assemblyProvider);
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Build(services, null!));
    }

    [Fact]
    public void Build_InvokesPostPluginRegistrationCallbacks()
    {
        // Arrange
        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [],
            () => []);

        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var builder = new ServiceProviderBuilder(populator, assemblyProvider);
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        
        var callbackInvoked = false;
        var callbacks = new List<Action<IServiceCollection>>
        {
            _ => callbackInvoked = true
        };

        // Act
        var provider = builder.Build(services, config, callbacks);

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void ConfigurePostBuildServiceCollectionPlugins_ThrowsOnNullProvider()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var builder = new ServiceProviderBuilder(populator, assemblyProvider);
        var config = new ConfigurationBuilder().Build();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            builder.ConfigurePostBuildServiceCollectionPlugins(null!, config));
    }

    [Fact]
    public void ConfigurePostBuildServiceCollectionPlugins_ThrowsOnNullConfig()
    {
        // Arrange
        var populator = new TestServiceCollectionPopulator();
        var assemblyProvider = new TestAssemblyProvider();
        var builder = new ServiceProviderBuilder(populator, assemblyProvider);
        var provider = new ServiceCollection().BuildServiceProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            builder.ConfigurePostBuildServiceCollectionPlugins(provider, null!));
    }

    private sealed class TestServiceCollectionPopulator : IServiceCollectionPopulator
    {
        public IServiceCollection RegisterToServiceCollection(
            IServiceCollection services,
            IConfiguration configuration,
            IReadOnlyList<System.Reflection.Assembly> assemblies)
        {
            // No-op for testing
            return services;
        }
    }

    private sealed class TestAssemblyProvider : IAssemblyProvider
    {
        private readonly System.Reflection.Assembly[] _assemblies;

        public TestAssemblyProvider(params System.Reflection.Assembly[] assemblies)
        {
            _assemblies = assemblies.Length > 0 ? assemblies : [];
        }

        public IReadOnlyList<System.Reflection.Assembly> GetCandidateAssemblies() => _assemblies;
    }
}
