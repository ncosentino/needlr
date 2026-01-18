using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

public sealed class ServiceResolutionParityTests
{
    private readonly IServiceProvider _reflectionProvider;
    private readonly IServiceProvider _generatedProvider;

    public ServiceResolutionParityTests()
    {
        _reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        _generatedProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();
    }

    [Fact]
    public void Parity_IMyAutomaticService_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<IMyAutomaticService>();
        var generatedService = _generatedProvider.GetService<IMyAutomaticService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal(reflectionService.GetType(), generatedService.GetType());
    }

    [Fact]
    public void Parity_IMyAutomaticService2_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<IMyAutomaticService2>();
        var generatedService = _generatedProvider.GetService<IMyAutomaticService2>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal(reflectionService.GetType(), generatedService.GetType());
    }

    [Fact]
    public void Parity_MyAutomaticService_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<MyAutomaticService>();
        var generatedService = _generatedProvider.GetService<MyAutomaticService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
    }

    [Fact]
    public void Parity_IInterfaceWithMultipleImplementations_BothProvidersResolveEnumerable()
    {
        var reflectionInstances = _reflectionProvider
            .GetService<IEnumerable<IInterfaceWithMultipleImplementations>>()
            ?.ToList();
        var generatedInstances = _generatedProvider
            .GetService<IEnumerable<IInterfaceWithMultipleImplementations>>()
            ?.ToList();

        Assert.NotNull(reflectionInstances);
        Assert.NotNull(generatedInstances);
        Assert.NotEmpty(reflectionInstances);
        Assert.NotEmpty(generatedInstances);

        Assert.Equal(reflectionInstances.Count, generatedInstances.Count);

        var reflectionTypes = reflectionInstances.Select(x => x.GetType()).OrderBy(t => t.Name).ToList();
        var generatedTypes = generatedInstances.Select(x => x.GetType()).OrderBy(t => t.Name).ToList();
        Assert.Equal(reflectionTypes, generatedTypes);
    }

    [Fact]
    public void Parity_ImplementationA_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<ImplementationA>();
        var generatedService = _generatedProvider.GetService<ImplementationA>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
    }

    [Fact]
    public void Parity_ImplementationB_BothProvidersResolve()
    {
        var reflectionService = _reflectionProvider.GetService<ImplementationB>();
        var generatedService = _generatedProvider.GetService<ImplementationB>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
    }

    [Fact]
    public void Parity_LazyIMyAutomaticService_BothProvidersResolve()
    {
        var reflectionLazy = _reflectionProvider.GetService<Lazy<IMyAutomaticService>>();
        var generatedLazy = _generatedProvider.GetService<Lazy<IMyAutomaticService>>();

        Assert.NotNull(reflectionLazy);
        Assert.NotNull(generatedLazy);
        Assert.NotNull(reflectionLazy.Value);
        Assert.NotNull(generatedLazy.Value);
        Assert.Equal(reflectionLazy.Value.GetType(), generatedLazy.Value.GetType());
    }

    [Fact]
    public void Parity_LazyMyAutomaticService_BothProvidersResolve()
    {
        var reflectionLazy = _reflectionProvider.GetService<Lazy<MyAutomaticService>>();
        var generatedLazy = _generatedProvider.GetService<Lazy<MyAutomaticService>>();

        Assert.NotNull(reflectionLazy);
        Assert.NotNull(generatedLazy);
        Assert.NotNull(reflectionLazy.Value);
        Assert.NotNull(generatedLazy.Value);
    }
}
