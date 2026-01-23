using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.AssemblyOrdering;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify assembly ordering behavior is identical between
/// reflection and source-gen paths using the unified OrderAssemblies API.
/// </summary>
public sealed class AssemblyOrderingParityTests
{
    [Fact]
    public void OrderAssemblies_SameApiForBothPaths()
    {
        // Arrange - Same configuration action used for both paths
        static void ConfigureOrdering(AssemblyOrderBuilder order)
        {
            order
                .By(a => a.Name.Contains("IntegrationTests"))
                .ThenBy(a => a.Name.EndsWith(".Core"));
        }

        // Act
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .OrderAssemblies(ConfigureOrdering)
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(ConfigureOrdering)
            .BuildServiceProvider();

        // Assert - Both use the same API and build successfully
        Assert.NotNull(reflectionProvider);
        Assert.NotNull(sourceGenProvider);
    }

    [Fact]
    public void OrderAssemblies_BothPathsResolveSameServiceType()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("IntegrationTests")))
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("IntegrationTests")))
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<IMyAutomaticService>();
        var sourceGenService = sourceGenProvider.GetService<IMyAutomaticService>();

        // Assert
        Assert.NotNull(reflectionService);
        Assert.NotNull(sourceGenService);
        Assert.Equal(reflectionService.GetType().FullName, sourceGenService.GetType().FullName);
    }

    [Fact]
    public void OrderAssemblies_BothPathsPlaceMatchingAssembliesFirst()
    {
        // Arrange
        var reflectionSyringe = new Syringe()
            .UsingReflection()
            .OrderAssemblies(order => order.By(a => a.Name.Contains("IntegrationTests")));

        var sourceGenSyringe = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order.By(a => a.Name.Contains("IntegrationTests")));

        // Act
        var reflectionAssemblies = reflectionSyringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        var sourceGenAssemblies = sourceGenSyringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert - Both paths should have IntegrationTests first
        Assert.NotEmpty(reflectionAssemblies);
        Assert.NotEmpty(sourceGenAssemblies);
        Assert.Contains("IntegrationTests", reflectionAssemblies[0].GetName().Name);
        Assert.Contains("IntegrationTests", sourceGenAssemblies[0].GetName().Name);
    }

    [Fact]
    public void OrderAssemblies_BothPathsPlaceMatchingAssembliesLast()
    {
        // Arrange - Put IntegrationTests last
        var reflectionSyringe = new Syringe()
            .UsingReflection()
            .OrderAssemblies(order => order.By(a => !a.Name.Contains("IntegrationTests")));

        var sourceGenSyringe = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order.By(a => !a.Name.Contains("IntegrationTests")));

        // Act
        var reflectionAssemblies = reflectionSyringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        var sourceGenAssemblies = sourceGenSyringe.GetOrCreateAssemblyProvider()
            .GetCandidateAssemblies()
            .ToList();

        // Assert - Both paths should have IntegrationTests last
        Assert.NotEmpty(reflectionAssemblies);
        Assert.NotEmpty(sourceGenAssemblies);
        Assert.Contains("IntegrationTests", reflectionAssemblies[^1].GetName().Name);
        Assert.Contains("IntegrationTests", sourceGenAssemblies[^1].GetName().Name);
    }

    [Fact]
    public void UseLibTestEntryOrdering_WorksForBothPaths()
    {
        // Act
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UseLibTestEntryOrdering()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UseLibTestEntryOrdering()
            .BuildServiceProvider();

        // Assert
        Assert.NotNull(reflectionProvider);
        Assert.NotNull(sourceGenProvider);
    }

    [Fact]
    public void UseTestsLastOrdering_WorksForBothPaths()
    {
        // Act
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UseTestsLastOrdering()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UseTestsLastOrdering()
            .BuildServiceProvider();

        // Assert
        Assert.NotNull(reflectionProvider);
        Assert.NotNull(sourceGenProvider);
    }

    [Fact]
    public void OrderAssemblies_PresetBuilder_WorksForBothPaths()
    {
        // Arrange
        var preset = AssemblyOrder.TestsLast();

        // Act
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .OrderAssemblies(preset)
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(preset)
            .BuildServiceProvider();

        // Assert
        Assert.NotNull(reflectionProvider);
        Assert.NotNull(sourceGenProvider);
    }
}
