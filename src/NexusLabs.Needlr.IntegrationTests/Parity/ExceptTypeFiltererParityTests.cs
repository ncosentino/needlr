using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests for Except&lt;T&gt;() type filterer between reflection and source-gen.
/// Verifies that types excluded via Except&lt;T&gt;() are NOT registered in either path.
/// </summary>
public sealed class ExceptTypeFiltererParityTests
{
    [Fact]
    public void Except_ExcludableServiceA_NotRegisteredInReflection()
    {
        // Arrange
        var provider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        // Act
        var service = provider.GetService<ExcludableServiceA>();

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void Except_ExcludableServiceA_NotRegisteredInSourceGen()
    {
        // Arrange
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        // Act
        var service = provider.GetService<ExcludableServiceA>();

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void Parity_Except_ExcludableServiceA_BothExclude()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<ExcludableServiceA>();
        var sourceGenService = sourceGenProvider.GetService<ExcludableServiceA>();

        // Assert - both should exclude
        Assert.Null(reflectionService);
        Assert.Null(sourceGenService);
    }

    [Fact]
    public void Parity_Except_ExcludableServiceB_BothExclude()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<ExcludableServiceB>();
        var sourceGenService = sourceGenProvider.GetService<ExcludableServiceB>();

        // Assert - both should exclude
        Assert.Null(reflectionService);
        Assert.Null(sourceGenService);
    }

    [Fact]
    public void Parity_Except_NonExcludableService_BothInclude()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<RegularServiceImpl>();
        var sourceGenService = sourceGenProvider.GetService<RegularServiceImpl>();

        // Assert - both should include (not excluded)
        Assert.NotNull(reflectionService);
        Assert.NotNull(sourceGenService);
    }

    [Fact]
    public void Parity_Except_MixedExcludableService_BothExclude()
    {
        // Arrange - MixedExcludableService implements BOTH IExcludableService and INonExcludableService
        // Since it implements IExcludableService, it should be excluded
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        // Act
        var reflectionService = reflectionProvider.GetService<MixedExcludableService>();
        var sourceGenService = sourceGenProvider.GetService<MixedExcludableService>();

        // Assert - both should exclude because type implements IExcludableService
        Assert.Null(reflectionService);
        Assert.Null(sourceGenService);
    }

    [Fact]
    public void Parity_Except_IExcludableServiceInterface_BothExclude()
    {
        // Arrange - verify the interface itself is not resolvable
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except<IExcludableService>())
            .BuildServiceProvider();

        // Act
        var reflectionServices = reflectionProvider.GetServices<IExcludableService>().ToList();
        var sourceGenServices = sourceGenProvider.GetServices<IExcludableService>().ToList();

        // Assert - no implementations should be registered
        Assert.Empty(reflectionServices);
        Assert.Empty(sourceGenServices);
    }

    [Fact]
    public void Parity_ExceptWithPredicate_BothExclude()
    {
        // Arrange - exclude by predicate (types with "Excludable" in name)
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except(t => t.Name.Contains("Excludable")))
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except(t => t.Name.Contains("Excludable")))
            .BuildServiceProvider();

        // Act
        var reflectionServiceA = reflectionProvider.GetService<ExcludableServiceA>();
        var reflectionServiceB = reflectionProvider.GetService<ExcludableServiceB>();
        var reflectionNonExcludable = reflectionProvider.GetService<RegularServiceImpl>();
        
        var sourceGenServiceA = sourceGenProvider.GetService<ExcludableServiceA>();
        var sourceGenServiceB = sourceGenProvider.GetService<ExcludableServiceB>();
        var sourceGenNonExcludable = sourceGenProvider.GetService<RegularServiceImpl>();

        // Assert
        Assert.Null(reflectionServiceA);
        Assert.Null(reflectionServiceB);
        Assert.NotNull(reflectionNonExcludable);
        
        Assert.Null(sourceGenServiceA);
        Assert.Null(sourceGenServiceB);
        Assert.NotNull(sourceGenNonExcludable);
    }

    [Fact]
    public void Parity_MultipleExcept_BothExcludeAll()
    {
        // Arrange - chain multiple Except calls
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .Except<IExcludableService>()
                .Except<INonExcludableService>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .Except<IExcludableService>()
                .Except<INonExcludableService>())
            .BuildServiceProvider();

        // Act
        var reflectionExcludable = reflectionProvider.GetService<ExcludableServiceA>();
        var reflectionNonExcludable = reflectionProvider.GetService<RegularServiceImpl>();
        
        var sourceGenExcludable = sourceGenProvider.GetService<ExcludableServiceA>();
        var sourceGenNonExcludable = sourceGenProvider.GetService<RegularServiceImpl>();

        // Assert - both types of services should be excluded
        Assert.Null(reflectionExcludable);
        Assert.Null(reflectionNonExcludable);
        
        Assert.Null(sourceGenExcludable);
        Assert.Null(sourceGenNonExcludable);
    }

    [Fact]
    public void Parity_NoExcept_BothIncludeAll()
    {
        // Arrange - baseline: no exclusions
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Act
        var reflectionExcludable = reflectionProvider.GetService<ExcludableServiceA>();
        var reflectionNonExcludable = reflectionProvider.GetService<RegularServiceImpl>();
        
        var sourceGenExcludable = sourceGenProvider.GetService<ExcludableServiceA>();
        var sourceGenNonExcludable = sourceGenProvider.GetService<RegularServiceImpl>();

        // Assert - all should be included
        Assert.NotNull(reflectionExcludable);
        Assert.NotNull(reflectionNonExcludable);
        
        Assert.NotNull(sourceGenExcludable);
        Assert.NotNull(sourceGenNonExcludable);
    }
}
