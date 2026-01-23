using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Scrutor;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

public sealed class ServiceCollectionBuilderExtensionsTests
{
    [Fact]
    public void Build_WithoutConfiguration_ReturnsServiceProvider()
    {
        // Arrange
        var syringe = new Syringe()
            .UsingReflection()
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NexusLabs.Needlr"))
                .Build());

        var serviceProviderBuilder = syringe.GetOrCreateServiceProviderBuilder(
            syringe.GetOrCreateServiceCollectionPopulator(
                syringe.GetOrCreateTypeRegistrar(),
                syringe.GetOrCreateTypeFilterer(),
                syringe.GetOrCreatePluginFactory()),
            syringe.GetOrCreateAssemblyProvider(),
            syringe.GetAdditionalAssemblies());

        // Act
        var provider = serviceProviderBuilder.Build();

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Build_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceProviderBuilder builder = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Build());
    }
}
