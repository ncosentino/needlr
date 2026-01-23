using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.AssemblyOrdering;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Tests for OrderAssemblies functionality using the source-gen path.
/// </summary>
public sealed class SourceGenOrderAssembliesTests
{
    [Fact]
    public void OrderAssemblies_MatchingAssembliesAppearFirst()
    {
        // Arrange - Order so IntegrationTests assembly comes first
        var syringe = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("IntegrationTests")));

        // Act
        var assemblies = syringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert - Must have multiple assemblies to prove ordering
        Assert.True(assemblies.Count >= 2,
            $"Expected at least 2 assemblies to verify ordering, got {assemblies.Count}");
        
        // Verify there's at least one non-matching assembly (proves sorting was needed)
        var nonMatchingExists = assemblies.Any(a => 
            a.GetName().Name?.Contains("IntegrationTests") != true);
        Assert.True(nonMatchingExists,
            "Test requires at least one assembly NOT containing 'IntegrationTests'");
        
        // Now verify the matching assembly is first
        var firstAssembly = assemblies[0];
        Assert.Contains("IntegrationTests", firstAssembly.GetName().Name);
    }

    [Fact]
    public void OrderAssemblies_NonMatchingAssembliesAppearLast()
    {
        // Arrange - Order so assemblies NOT containing "IntegrationTests" come first
        var syringe = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => !a.Name.Contains("IntegrationTests")));

        // Act
        var assemblies = syringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert - Must have multiple assemblies to prove ordering
        Assert.True(assemblies.Count >= 2,
            $"Expected at least 2 assemblies to verify ordering, got {assemblies.Count}");
        
        // Verify there's at least one non-IntegrationTests assembly (proves sorting happened)
        var nonIntegrationTestsExists = assemblies.Any(a => 
            a.GetName().Name?.Contains("IntegrationTests") != true);
        Assert.True(nonIntegrationTestsExists,
            "Test requires at least one assembly NOT containing 'IntegrationTests'");
        
        // Verify first assembly is NOT IntegrationTests (non-matching sorted first)
        var firstAssembly = assemblies[0];
        Assert.DoesNotContain("IntegrationTests", firstAssembly.GetName().Name);
        
        // Verify IntegrationTests is last
        var lastAssembly = assemblies[^1];
        Assert.Contains("IntegrationTests", lastAssembly.GetName().Name);
    }

    [Fact]
    public void OrderAssemblies_TieredOrderingAppliesCorrectly()
    {
        // Arrange - Create tiered ordering: Injection first, then IntegrationTests
        var syringe = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("Injection") && !a.Name.Contains("IntegrationTests"))
                .ThenBy(a => a.Name.Contains("IntegrationTests")));

        // Act
        var assemblies = syringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert
        Assert.True(assemblies.Count >= 2, "Expected at least 2 assemblies");
        
        var injectionIndex = assemblies
            .Select((a, i) => (a, i))
            .Where(x => x.a.GetName().Name?.Contains("Injection") == true 
                     && x.a.GetName().Name?.Contains("IntegrationTests") != true)
            .Select(x => x.i)
            .FirstOrDefault(-1);

        var integrationTestsIndex = assemblies
            .Select((a, i) => (a, i))
            .Where(x => x.a.GetName().Name?.Contains("IntegrationTests") == true)
            .Select(x => x.i)
            .FirstOrDefault(-1);

        Assert.NotEqual(-1, injectionIndex);
        Assert.NotEqual(-1, integrationTestsIndex);
        Assert.True(injectionIndex < integrationTestsIndex,
            $"Injection assembly (index {injectionIndex}) should come before IntegrationTests (index {integrationTestsIndex})");
    }

    [Fact]
    public void OrderAssemblies_WithoutOrdering_AssembliesAreDiscovered()
    {
        // Arrange - No ordering applied
        var syringeWithoutOrdering = new Syringe()
            .UsingSourceGen();

        var syringeWithOrdering = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order.By(a => a.Name.Contains("IntegrationTests")));

        // Act
        var assembliesWithout = syringeWithoutOrdering.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();
        
        var assembliesWith = syringeWithOrdering.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert - Must have at least 2 assemblies for ordering to be meaningful
        Assert.True(assembliesWithout.Count >= 2,
            $"Expected at least 2 assemblies, got {assembliesWithout.Count}");
        Assert.Equal(assembliesWithout.Count, assembliesWith.Count);
        
        // Verify same assemblies exist in both (ordering doesn't add/remove)
        var namesWithout = assembliesWithout.Select(a => a.GetName().Name).OrderBy(n => n).ToList();
        var namesWith = assembliesWith.Select(a => a.GetName().Name).OrderBy(n => n).ToList();
        Assert.Equal(namesWithout, namesWith);
    }

    [Fact]
    public void OrderAssemblies_OrderingChangesAssemblyOrder()
    {
        // Arrange
        var syringeIntegrationFirst = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order.By(a => a.Name.Contains("IntegrationTests")));

        var syringeIntegrationLast = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order.By(a => !a.Name.Contains("IntegrationTests")));

        // Act
        var assembliesFirst = syringeIntegrationFirst.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        var assembliesLast = syringeIntegrationLast.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert - Must have at least 2 assemblies
        Assert.True(assembliesFirst.Count >= 2,
            $"Need at least 2 assemblies to verify ordering, got {assembliesFirst.Count}");
        Assert.Equal(assembliesFirst.Count, assembliesLast.Count);
        
        // Verify both matching and non-matching assemblies exist
        Assert.Contains(assembliesFirst, a => a.GetName().Name?.Contains("IntegrationTests") == true);
        Assert.Contains(assembliesFirst, a => a.GetName().Name?.Contains("IntegrationTests") != true);
        
        // IntegrationTests should be first in one ordering
        Assert.Contains("IntegrationTests", assembliesFirst[0].GetName().Name);
        // IntegrationTests should NOT be first in the opposite ordering
        Assert.DoesNotContain("IntegrationTests", assembliesLast[0].GetName().Name);
        // IntegrationTests should be last in the opposite ordering
        Assert.Contains("IntegrationTests", assembliesLast[^1].GetName().Name);
    }

    [Fact]
    public void UseLibTestEntryOrdering_AppliesOrdering()
    {
        // Arrange
        var syringe = new Syringe()
            .UsingSourceGen()
            .UseLibTestEntryOrdering();

        // Act
        var assemblies = syringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert - Just verify ordering was applied (assemblies are discovered)
        Assert.NotEmpty(assemblies);
        
        // The IntegrationTests assembly (a test assembly) should come after non-test assemblies
        var integrationTestsIndex = assemblies
            .Select((a, i) => (a, i))
            .Where(x => x.a.GetName().Name?.Contains("IntegrationTests") == true)
            .Select(x => x.i)
            .First();

        var nonTestIndices = assemblies
            .Select((a, i) => (a, i))
            .Where(x => x.a.GetName().Name?.Contains("Tests") != true)
            .Select(x => x.i)
            .ToList();

        // Non-test assemblies should exist and come before IntegrationTests
        Assert.NotEmpty(nonTestIndices);
        Assert.True(nonTestIndices.Max() < integrationTestsIndex,
            "Test assemblies should come after non-test assemblies");
    }

    [Fact]
    public void OrderAssemblies_ServicesResolveAfterOrdering()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("IntegrationTests")))
            .BuildServiceProvider();

        // Assert - Service resolution still works after ordering
        var service = provider.GetService<IMyAutomaticService>();
        Assert.NotNull(service);
        Assert.IsType<MyAutomaticService>(service);
    }
}
