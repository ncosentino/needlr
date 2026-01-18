using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

public sealed class ServiceProviderBuilderTests
{
    [Fact]
    public void Build_WithEmptyServiceCollection_ShouldRegisterConfigurationInServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestSetting"] = "TestValue"
            })
            .Build();

        var serviceCollectionPopulator = new ServiceCollectionPopulator(
            new ReflectionTypeRegistrar(),
            new ReflectionTypeFilterer());

        var assemblyProvider = new AssembyProviderBuilder().Build();
        var serviceProviderBuilder = new ServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider);

        var serviceProvider = serviceProviderBuilder.Build(configuration);

        var resolvedConfiguration = serviceProvider.GetService<IConfiguration>();
        Assert.NotNull(resolvedConfiguration);
        Assert.Equal("TestValue", resolvedConfiguration["TestSetting"]);
    }

    [Fact]
    public void Build_WithCustomServiceCollection_ShouldRegisterConfigurationInServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestSetting"] = "TestValue"
            })
            .Build();

        var services = new ServiceCollection();
        var serviceCollectionPopulator = new ServiceCollectionPopulator(
            new ReflectionTypeRegistrar(),
            new ReflectionTypeFilterer());

        var assemblyProvider = new AssembyProviderBuilder().Build();
        var serviceProviderBuilder = new ServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider);

        var serviceProvider = serviceProviderBuilder.Build(services, configuration);

        var resolvedConfiguration = serviceProvider.GetService<IConfiguration>();
        Assert.NotNull(resolvedConfiguration);
        Assert.Equal("TestValue", resolvedConfiguration["TestSetting"]);
    }
}