using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Tests for keyed services parity between reflection and source-generated paths.
/// Ensures that [FromKeyedServices] attribute is properly handled in both scenarios.
/// </summary>
public sealed class KeyedServicesParityTests
{
    [Fact]
    public void Parity_KeyedServiceParameter_BothProvidersResolve()
    {
        // Arrange - Plugin registers keyed services, discovered automatically
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var generatedProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Resolve service that depends on keyed service
        var reflectionService = reflectionProvider.GetService<ServiceWithKeyedDependency>();
        var generatedService = generatedProvider.GetService<ServiceWithKeyedDependency>();

        // Assert
        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal("primary", reflectionService.GetPaymentProcessorName());
        Assert.Equal("primary", generatedService.GetPaymentProcessorName());
    }

    [Fact]
    public void Parity_MixedKeyedAndUnkeyedParameters_BothProvidersResolve()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var generatedProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<ServiceWithMixedDependencies>();
        var generatedService = generatedProvider.GetService<ServiceWithMixedDependencies>();

        // Assert
        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal("backup", reflectionService.GetPaymentProcessorName());
        Assert.NotNull(reflectionService.GetLogger());
        Assert.Equal("backup", generatedService.GetPaymentProcessorName());
        Assert.NotNull(generatedService.GetLogger());
    }

    [Fact]
    public void Parity_MultipleKeyedParameters_BothProvidersResolve()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var generatedProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<ServiceWithMultipleKeyedDependencies>();
        var generatedService = generatedProvider.GetService<ServiceWithMultipleKeyedDependencies>();

        // Assert
        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal("primary", reflectionService.GetPrimaryProcessorName());
        Assert.Equal("backup", reflectionService.GetBackupProcessorName());
        Assert.Equal("primary", generatedService.GetPrimaryProcessorName());
        Assert.Equal("backup", generatedService.GetBackupProcessorName());
    }
}
