using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Tests that verify the zero-reflection configuration produces the same results
/// as the reflection-based configuration.
/// </summary>
public sealed class ZeroReflectionParityTests
{
    private readonly IServiceProvider _reflectionProvider;
    private readonly IServiceProvider _zeroReflectionProvider;

    public ZeroReflectionParityTests()
    {
        // Reflection-based configuration
        _reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Zero-reflection configuration using UsingGeneratedComponents
        _zeroReflectionProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
    }

    [Fact]
    public void Parity_ZeroReflection_ResolvesMyAutomaticService()
    {
        var reflectionService = _reflectionProvider.GetService<IMyAutomaticService>();
        var zeroReflectionService = _zeroReflectionProvider.GetService<IMyAutomaticService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(zeroReflectionService);
        Assert.Equal(reflectionService.GetType(), zeroReflectionService.GetType());
    }

    [Fact]
    public void Parity_ZeroReflection_ResolvesMyAutomaticService2()
    {
        var reflectionService = _reflectionProvider.GetService<IMyAutomaticService2>();
        var zeroReflectionService = _zeroReflectionProvider.GetService<IMyAutomaticService2>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(zeroReflectionService);
        Assert.Equal(reflectionService.GetType(), zeroReflectionService.GetType());
    }

    [Fact]
    public void Parity_ZeroReflection_ResolvesConcreteType()
    {
        var reflectionService = _reflectionProvider.GetService<MyAutomaticService>();
        var zeroReflectionService = _zeroReflectionProvider.GetService<MyAutomaticService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(zeroReflectionService);
    }

    [Fact]
    public void Parity_ZeroReflection_SingletonBehavior()
    {
        var service1 = _zeroReflectionProvider.GetService<IMyAutomaticService>();
        var service2 = _zeroReflectionProvider.GetService<IMyAutomaticService>();

        Assert.Same(service1, service2);
    }

    [Fact]
    public void Parity_ZeroReflection_DoNotAutoRegister_NotRegistered()
    {
        // MyManualService has [DoNotAutoRegister] attribute
        var reflectionService = _reflectionProvider.GetService<IMyManualService>();
        var zeroReflectionService = _zeroReflectionProvider.GetService<IMyManualService>();

        Assert.Null(reflectionService);
        Assert.Null(zeroReflectionService);
    }

    [Fact]
    public void ZeroReflection_ConfiguresAllComponents()
    {
        // Verify that UsingGeneratedComponents configures the registrar, filterer, and plugin factory
        var syringe = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        // Verify service provider can be built and services resolved
        var provider = syringe.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMyAutomaticService>());
        Assert.NotNull(provider.GetService<IMyAutomaticService2>());
        Assert.NotNull(provider.GetService<MyAutomaticService>());
    }

    [Fact]
    public void Parity_ZeroReflection_MultiInterfaceRegistration()
    {
        // MyAutomaticService implements both IMyAutomaticService and IMyAutomaticService2
        var service1 = _zeroReflectionProvider.GetService<IMyAutomaticService>();
        var service2 = _zeroReflectionProvider.GetService<IMyAutomaticService2>();

        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.Same(service1, service2);
    }
}
