using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen.Loaders;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Loaders;

public sealed class GeneratedAssemblyProviderTests
{
    [Fact]
    public void GetCandidateAssemblies_EmptyMetadataProviders_ReturnsRegistryParticipantAssembly()
    {
        var provider = new GeneratedAssemblyProvider(
            () => [],
            () => [],
            [typeof(ServiceCollection)]);

        var assembly = Assert.Single(provider.GetCandidateAssemblies());

        Assert.Same(typeof(ServiceCollection).Assembly, assembly);
    }

    [Fact]
    public void GetCandidateAssemblies_ParticipantsAppendAfterMetadataAndDeduplicate()
    {
        var injectable = new InjectableTypeInfo(typeof(string), []);
        var plugin = new PluginTypeInfo(
            typeof(GeneratedAssemblyProviderTests),
            [],
            static () => new object(),
            []);
        var provider = new GeneratedAssemblyProvider(
            () => [injectable],
            () => [plugin],
            [
                typeof(string),
                typeof(ServiceCollection),
                typeof(GeneratedAssemblyProviderTests)
            ]);

        var assemblies = provider.GetCandidateAssemblies();

        Assert.Equal(
            [
                typeof(string).Assembly,
                typeof(GeneratedAssemblyProviderTests).Assembly,
                typeof(ServiceCollection).Assembly
            ],
            assemblies);
    }

    [Fact]
    public void Constructor_NullRegistryParticipantTypes_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GeneratedAssemblyProvider(() => [], () => [], null!));

        Assert.Equal("registryParticipantTypes", exception.ParamName);
    }
}
