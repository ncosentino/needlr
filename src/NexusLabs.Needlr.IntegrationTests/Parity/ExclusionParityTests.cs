using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

public sealed class ExclusionParityTests
{
    private readonly IServiceProvider _reflectionProvider;
    private readonly IServiceProvider _generatedProvider;

    public ExclusionParityTests()
    {
        _reflectionProvider = new Syringe()
            .UsingDefaultTypeRegistrar()
            .BuildServiceProvider();

        _generatedProvider = new Syringe()
            .UsingGeneratedTypeRegistrar(
                NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes)
            .BuildServiceProvider();
    }

    [Fact]
    public void Parity_MyManualService_BothProvidersExclude()
    {
        var reflectionService = _reflectionProvider.GetService<MyManualService>();
        var generatedService = _generatedProvider.GetService<MyManualService>();

        Assert.Null(reflectionService);
        Assert.Null(generatedService);
    }

    [Fact]
    public void Parity_IMyManualService_BothProvidersExclude()
    {
        var reflectionService = _reflectionProvider.GetService<IMyManualService>();
        var generatedService = _generatedProvider.GetService<IMyManualService>();

        Assert.Null(reflectionService);
        Assert.Null(generatedService);
    }

    [Fact]
    public void Parity_TestServiceToBeDecorated_BothProvidersExclude()
    {
        var reflectionService = _reflectionProvider.GetService<TestServiceToBeDecorated>();
        var generatedService = _generatedProvider.GetService<TestServiceToBeDecorated>();

        Assert.Null(reflectionService);
        Assert.Null(generatedService);
    }
}
