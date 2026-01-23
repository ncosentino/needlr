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

    [Fact]
    public void OrderAssemblies_SortsAlphabetically_WhenConfiguredWithNameOrdering()
    {
        var builder = new AssemblyProviderBuilder()
            .OrderAssemblies(order => order
                .By(a => string.Compare(a.Name, "Z", StringComparison.Ordinal) < 0)); // all assemblies match

        var provider = builder.Build();
        var assemblies = provider.GetCandidateAssemblies();

        Assert.NotEmpty(assemblies);
    }

    [Fact]
    public void UseLibTestEntryOrdering_SortsCorrectly()
    {
        var builder = new AssemblyProviderBuilder()
            .UseLibTestEntryOrdering();

        var provider = builder.Build();
        var assemblies = provider.GetCandidateAssemblies().ToList();

        // Test assemblies should come after non-test assemblies
        var testAssemblyIndices = assemblies
            .Select((a, i) => (a, i))
            .Where(x => x.a.GetName().Name?.Contains("Tests", StringComparison.OrdinalIgnoreCase) == true)
            .Select(x => x.i)
            .ToList();

        var nonTestAssemblyIndices = assemblies
            .Select((a, i) => (a, i))
            .Where(x => x.a.GetName().Name?.Contains("Tests", StringComparison.OrdinalIgnoreCase) != true)
            .Select(x => x.i)
            .ToList();

        // All non-test assemblies should come before all test assemblies
        if (testAssemblyIndices.Count > 0 && nonTestAssemblyIndices.Count > 0)
        {
            Assert.True(nonTestAssemblyIndices.Max() < testAssemblyIndices.Min(),
                "Non-test assemblies should come before test assemblies");
        }
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
