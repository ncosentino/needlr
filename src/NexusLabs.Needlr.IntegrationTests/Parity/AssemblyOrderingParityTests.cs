using NexusLabs.Needlr.Injection.AssemblyOrdering;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Tests to verify that the AssemblyOrderBuilder produces consistent ordering
/// for both reflection (Assembly) and source-gen (string name) scenarios.
/// </summary>
public sealed class AssemblyOrderingParityTests
{
    [Fact]
    public void SortNames_WithNoRules_ReturnsOriginalOrder()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder();
        var names = new[] { "Zebra", "Alpha", "Middle" };

        // Act
        var result = builder.SortNames(names);

        // Assert - no rules, original order preserved
        Assert.Equal(names, result);
    }

    [Fact]
    public void SortNames_WithSingleRule_MatchedFirst_UnmatchedLast()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder()
            .By(a => a.Name.StartsWith("A", StringComparison.OrdinalIgnoreCase));
        var names = new[] { "Zebra", "Alpha", "Apple", "Middle" };

        // Act
        var result = builder.SortNames(names).ToList();

        // Assert
        // Alpha and Apple match, should come first (alphabetically within tier)
        Assert.Equal("Alpha", result[0]);
        Assert.Equal("Apple", result[1]);
        // Unmatched assemblies come last (alphabetically within tier)
        Assert.Equal("Middle", result[2]);
        Assert.Equal("Zebra", result[3]);
    }

    [Fact]
    public void SortNames_WithMultipleRules_TieredOrdering()
    {
        // Arrange - Libraries first, then Tests, then everything else
        var builder = new AssemblyOrderBuilder()
            .By(a => a.Name.EndsWith(".Core", StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Name.EndsWith(".Services", StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase));

        var names = new[]
        {
            "MyApp.Tests",
            "MyApp.Core",
            "MyApp.Services",
            "MyApp.Api",
            "MyApp.Integration.Tests"
        };

        // Act
        var result = builder.SortNames(names).ToList();

        // Assert
        // Tier 0: .Core
        Assert.Equal("MyApp.Core", result[0]);
        // Tier 1: .Services
        Assert.Equal("MyApp.Services", result[1]);
        // Tier 2: Tests (alphabetically within tier)
        Assert.Equal("MyApp.Integration.Tests", result[2]);
        Assert.Equal("MyApp.Tests", result[3]);
        // Unmatched
        Assert.Equal("MyApp.Api", result[4]);
    }

    [Fact]
    public void SortNames_LibTestEntryPattern_TestsComeLast()
    {
        // Arrange - Simulating LibTestEntry pattern
        var builder = new AssemblyOrderBuilder()
            .By(a => !a.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase));

        var names = new[]
        {
            "MyApp.Unit.Tests",
            "MyApp.Core",
            "MyApp.Integration.Tests",
            "MyApp.Services"
        };

        // Act
        var result = builder.SortNames(names).ToList();

        // Assert
        // Non-tests first (alphabetically)
        Assert.Equal("MyApp.Core", result[0]);
        Assert.Equal("MyApp.Services", result[1]);
        // Tests last (alphabetically)
        Assert.Equal("MyApp.Integration.Tests", result[2]);
        Assert.Equal("MyApp.Unit.Tests", result[3]);
    }

    [Fact]
    public void SortNames_UnmatchedAssembliesAlwaysLast()
    {
        // Arrange - Only match specific patterns, unmatched go last
        var builder = new AssemblyOrderBuilder()
            .By(a => a.Name.StartsWith("Priority", StringComparison.OrdinalIgnoreCase));

        var names = new[]
        {
            "OtherAssembly",
            "PriorityFirst",
            "AnotherOne",
            "ZebraLib",
            "PrioritySecond"
        };

        // Act
        var result = builder.SortNames(names).ToList();

        // Assert
        // Priority assemblies first (alphabetically within tier)
        Assert.Equal("PriorityFirst", result[0]);
        Assert.Equal("PrioritySecond", result[1]);
        // Unmatched assemblies last (alphabetically)
        Assert.Equal("AnotherOne", result[2]);
        Assert.Equal("OtherAssembly", result[3]);
        Assert.Equal("ZebraLib", result[4]);
    }

    [Fact]
    public void SortNames_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder()
            .By(a => true);

        // Act
        var result = builder.SortNames([]);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SortNames_FirstMatchingRuleWins()
    {
        // Arrange - If an assembly matches multiple rules, first match wins
        var builder = new AssemblyOrderBuilder()
            .By(a => a.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)) // Tier 0
            .ThenBy(a => a.Name.Contains("App", StringComparison.OrdinalIgnoreCase)) // Tier 1
            .ThenBy(a => true); // Tier 2 - matches everything

        var names = new[] { "MyApp.Core", "MyApp.Services", "External.Lib" };

        // Act
        var result = builder.SortNames(names).ToList();

        // Assert
        // MyApp.Core matches both Core and App, but Core rule comes first
        Assert.Equal("MyApp.Core", result[0]); // Tier 0 (Core)
        Assert.Equal("MyApp.Services", result[1]); // Tier 1 (App)
        Assert.Equal("External.Lib", result[2]); // Tier 2 (catch-all)
    }

    [Fact]
    public void ThenBy_WithoutBy_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            builder.ThenBy(a => true));
    }

    [Fact]
    public void By_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            builder.By(null!));
    }

    [Fact]
    public void SortNames_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder()
            .By(a => true);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            builder.SortNames(null!));
    }

    [Fact]
    public void Rules_ReturnsConfiguredRules()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder()
            .By(a => a.Name.StartsWith("A"))
            .ThenBy(a => a.Name.StartsWith("B"))
            .ThenBy(a => a.Name.StartsWith("C"));

        // Act
        var rules = builder.Rules;

        // Assert
        Assert.Equal(3, rules.Count);
        Assert.Equal(0, rules[0].Tier);
        Assert.Equal(1, rules[1].Tier);
        Assert.Equal(2, rules[2].Tier);
    }

    [Fact]
    public void Sort_WithAssemblies_WorksLikeReflection()
    {
        // Arrange
        var builder = new AssemblyOrderBuilder()
            .By(a => a.Name.Contains("Needlr", StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase));

        var currentAssembly = typeof(AssemblyOrderingParityTests).Assembly;
        var assemblies = new[] { currentAssembly };

        // Act
        var result = builder.Sort(assemblies).ToList();

        // Assert
        Assert.Single(result);
        Assert.Same(currentAssembly, result[0]);
    }

    [Fact]
    public void AssemblyInfo_FromStrings_CreatesValidInfo()
    {
        // Act
        var info = AssemblyInfo.FromStrings("MyAssembly", "/path/to/MyAssembly.dll");

        // Assert
        Assert.Equal("MyAssembly", info.Name);
        Assert.Equal("/path/to/MyAssembly.dll", info.Location);
    }

    [Fact]
    public void AssemblyInfo_FromStrings_WithoutLocation_UsesEmptyString()
    {
        // Act
        var info = AssemblyInfo.FromStrings("MyAssembly");

        // Assert
        Assert.Equal("MyAssembly", info.Name);
        Assert.Equal("", info.Location);
    }
}
