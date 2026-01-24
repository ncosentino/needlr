using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.AssemblyOrdering;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Reflection;

/// <summary>
/// Tests for OrderAssemblies functionality using the reflection path.
/// These tests use MatchingAssemblies to discover multiple NexusLabs assemblies.
/// </summary>
public sealed class ReflectionOrderAssembliesTests
{
    private static ConfiguredSyringe CreateSyringeWithMultipleAssemblies()
    {
        // UsingReflection must come first, then override with custom assembly provider
        return new Syringe()
            .UsingReflection()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(name => name.Contains("NexusLabs", StringComparison.OrdinalIgnoreCase))
                .Build());
    }

    [Fact]
    public void OrderAssemblies_MatchingAssembliesAppearFirst()
    {
        // Arrange - Order so IntegrationTests assembly comes first
        var syringe = CreateSyringeWithMultipleAssemblies()
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
        var syringe = CreateSyringeWithMultipleAssemblies()
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
        var syringe = CreateSyringeWithMultipleAssemblies()
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
    public void OrderAssemblies_OrderingChangesAssemblyOrder()
    {
        // Arrange
        var syringeIntegrationFirst = CreateSyringeWithMultipleAssemblies()
            .OrderAssemblies(order => order.By(a => a.Name.Contains("IntegrationTests")));

        var syringeIntegrationLast = CreateSyringeWithMultipleAssemblies()
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
    public void UseLibTestEntryOrdering_TestAssembliesComeAfterNonTestAssemblies()
    {
        // Arrange
        var syringe = CreateSyringeWithMultipleAssemblies()
            .UseLibTestEntryOrdering();

        // Act
        var assemblies = syringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert
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

        Assert.NotEmpty(testAssemblyIndices);
        Assert.NotEmpty(nonTestAssemblyIndices);
        Assert.True(
            nonTestAssemblyIndices.Max() < testAssemblyIndices.Min(),
            "Non-test assemblies should come before test assemblies");
    }

    [Fact]
    public void UseTestsLastOrdering_TestAssembliesComeAfterNonTestAssemblies()
    {
        // Arrange
        var syringe = CreateSyringeWithMultipleAssemblies()
            .UseTestsLastOrdering();

        // Act
        var assemblies = syringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert
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

        Assert.NotEmpty(testAssemblyIndices);
        Assert.NotEmpty(nonTestAssemblyIndices);
        Assert.True(
            nonTestAssemblyIndices.Max() < testAssemblyIndices.Min(),
            "Non-test assemblies should come before test assemblies");
    }

    [Fact]
    public void OrderAssemblies_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var syringe = CreateSyringeWithMultipleAssemblies();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            syringe.OrderAssemblies((Action<AssemblyOrderBuilder>)null!));
    }

    [Fact]
    public void OrderAssemblies_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var syringe = CreateSyringeWithMultipleAssemblies();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            syringe.OrderAssemblies((AssemblyOrderBuilder)null!));
    }
}
