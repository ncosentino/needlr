using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

public sealed class SingletonBehaviorParityTests
{
    private readonly IServiceProvider _reflectionProvider;
    private readonly IServiceProvider _generatedProvider;

    public SingletonBehaviorParityTests()
    {
        _reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        _generatedProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
    }

    [Fact]
    public void Parity_SingletonBehavior_IMyAutomaticService_SameInstance()
    {
        var r1 = _reflectionProvider.GetService<IMyAutomaticService>();
        var r2 = _reflectionProvider.GetService<IMyAutomaticService>();
        Assert.Same(r1, r2);

        var g1 = _generatedProvider.GetService<IMyAutomaticService>();
        var g2 = _generatedProvider.GetService<IMyAutomaticService>();
        Assert.Same(g1, g2);
    }

    [Fact]
    public void Parity_SingletonBehavior_InterfacesShareInstance()
    {
        var r1 = _reflectionProvider.GetService<IMyAutomaticService>();
        var r2 = _reflectionProvider.GetService<IMyAutomaticService2>();
        Assert.Same(r1, r2);

        var g1 = _generatedProvider.GetService<IMyAutomaticService>();
        var g2 = _generatedProvider.GetService<IMyAutomaticService2>();
        Assert.Same(g1, g2);
    }

    [Fact]
    public void Parity_SingletonBehavior_ConcreteAndInterfaceShareInstance()
    {
        var r1 = _reflectionProvider.GetService<MyAutomaticService>();
        var r2 = _reflectionProvider.GetService<IMyAutomaticService>();
        Assert.Same(r1, r2);

        var g1 = _generatedProvider.GetService<MyAutomaticService>();
        var g2 = _generatedProvider.GetService<IMyAutomaticService>();
        Assert.Same(g1, g2);
    }
}
