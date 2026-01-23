using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify assembly ordering behavior is identical between
/// reflection and source-gen paths using the same By().ThenBy() syntax.
/// </summary>
public sealed class AssemblyOrderingParityTests
{
    [Fact]
    public void OrderAssemblies_BySinglePredicate_ReflectionBuildsSuccessfully()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingReflection()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("Needlr")))
            .BuildServiceProvider();

        // Assert - Provider is built successfully with ordering
        Assert.NotNull(provider);
    }

    [Fact]
    public void OrderAssemblies_BySinglePredicate_SourceGenBuildsSuccessfully()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("Needlr")))
            .BuildServiceProvider();

        // Assert - Provider is built successfully with ordering
        Assert.NotNull(provider);
    }

    [Fact]
    public void OrderAssemblies_ByThenBy_ReflectionAndSourceGenResolveSameTypes()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .OrderAssemblies(order => order
                .By(a => a.Name.EndsWith(".Core"))
                .ThenBy(a => a.Name.Contains("Injection")))
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.EndsWith(".Core"))
                .ThenBy(a => a.Name.Contains("Injection")))
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<IMyAutomaticService>();
        var sourceGenService = sourceGenProvider.GetService<IMyAutomaticService>();

        // Assert - Both should resolve the same service type
        Assert.NotNull(reflectionService);
        Assert.NotNull(sourceGenService);
        Assert.Equal(reflectionService.GetType().FullName, sourceGenService.GetType().FullName);
    }

    [Fact]
    public void OrderAssemblies_MultiplePredicates_ReflectionAndSourceGenResolveSameTypes()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("Needlr.Core"))
                .ThenBy(a => a.Name.Contains("Needlr.Injection"))
                .ThenBy(a => a.Name.Contains("Tests")))
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("Needlr.Core"))
                .ThenBy(a => a.Name.Contains("Needlr.Injection"))
                .ThenBy(a => a.Name.Contains("Tests")))
            .BuildServiceProvider();

        // Act
        var reflectionServices = reflectionProvider.GetServices<IMyAutomaticService>().ToList();
        var sourceGenServices = sourceGenProvider.GetServices<IMyAutomaticService>().ToList();

        // Assert - Both should have the same count
        Assert.Equal(reflectionServices.Count, sourceGenServices.Count);
    }

    [Fact]
    public void OrderAssemblies_NoMatchingPredicates_ReflectionAndSourceGenStillWork()
    {
        // Arrange & Act - Use predicates that won't match any assembly
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("NonExistentAssemblyName")))
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .OrderAssemblies(order => order
                .By(a => a.Name.Contains("NonExistentAssemblyName")))
            .BuildServiceProvider();

        // Assert - Both should still work (assemblies without matches go to end tier)
        var reflectionService = reflectionProvider.GetService<IMyAutomaticService>();
        var sourceGenService = sourceGenProvider.GetService<IMyAutomaticService>();
        
        Assert.NotNull(reflectionService);
        Assert.NotNull(sourceGenService);
    }

    [Fact]
    public void OrderAssemblies_ReflectionAndSourceGen_UseSameFluentSyntax()
    {
        // This test proves the API is identical for both paths

        // Arrange - Create the same ordering configuration
        static void ConfigureOrdering(Injection.AssemblyOrdering.AssemblyOrderBuilder order)
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

        // Assert - Both use the same configuration function
        Assert.NotNull(reflectionProvider);
        Assert.NotNull(sourceGenProvider);
    }
}
