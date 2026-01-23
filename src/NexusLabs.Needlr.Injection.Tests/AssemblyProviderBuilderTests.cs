using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

public sealed class AssemblyProviderBuilderTests
{
    [Fact]
    public void Build_WithDefaultSettings_ReturnsAssemblyProvider()
    {
        var builder = new AssemblyProviderBuilder();

        var provider = builder.Build();

        Assert.NotNull(provider);
    }

    [Fact]
    public void Build_ReturnsProviderWithAssemblies()
    {
        var builder = new AssemblyProviderBuilder();

        var provider = builder.Build();
        var assemblies = provider.GetCandidateAssemblies();

        Assert.NotNull(assemblies);
        Assert.NotEmpty(assemblies);
    }

    [Fact]
    public void UseLoader_WithNullLoader_ThrowsArgumentNullException()
    {
        var builder = new AssemblyProviderBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.UseLoader(null!));
    }

    [Fact]
    public void Build_UsesCustomLoader()
    {
        var mockLoader = new MockAssemblyLoader();
        var builder = new AssemblyProviderBuilder()
            .UseLoader(mockLoader);

        var provider = builder.Build();
        var assemblies = provider.GetCandidateAssemblies();

        Assert.True(mockLoader.WasCalled);
    }

    [Fact]
    public void MatchingAssemblies_FiltersCorrectly()
    {
        var builder = new AssemblyProviderBuilder()
            .MatchingAssemblies(path => path.Contains("NexusLabs"));

        var provider = builder.Build();
        var assemblies = provider.GetCandidateAssemblies();

        Assert.All(assemblies, a => Assert.Contains("NexusLabs", a.Location));
    }

    private sealed class MockAssemblyLoader : IAssemblyLoader
    {
        public bool WasCalled { get; private set; }

        public IReadOnlyList<System.Reflection.Assembly> LoadAssemblies(bool continueOnAssemblyError)
        {
            WasCalled = true;
            return [typeof(AssemblyProviderBuilderTests).Assembly];
        }
    }
}
