using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests to verify that UsingOnlyAsTransient lifetime override
/// works identically in both reflection and source-gen modes.
/// </summary>
public sealed class UsingOnlyAsTransientParityTests
{
    [Fact]
    public void Reflection_UsingOnlyAsTransient_OverridesDefaultLifetime()
    {
        // Arrange - SingletonJobService has [InjectAsSingleton] but we override ITestJob to transient
        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        // Act - resolve twice and check if same instance
        var job1 = serviceProvider.GetRequiredService<SingletonJobService>();
        var job2 = serviceProvider.GetRequiredService<SingletonJobService>();

        // Assert - should be DIFFERENT instances (transient)
        Assert.NotSame(job1, job2);
    }

    [Fact]
    public void SourceGen_UsingOnlyAsTransient_OverridesDefaultLifetime()
    {
        // Arrange - SingletonJobService has [InjectAsSingleton] but we override ITestJob to transient
        var serviceProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        // Act - resolve twice and check if same instance
        var job1 = serviceProvider.GetRequiredService<SingletonJobService>();
        var job2 = serviceProvider.GetRequiredService<SingletonJobService>();

        // Assert - should be DIFFERENT instances (transient)
        Assert.NotSame(job1, job2);
    }

    [Fact]
    public void Parity_UsingOnlyAsTransient_BothOverrideToTransient()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        // Act
        var reflectionJob1 = reflectionProvider.GetRequiredService<SingletonJobService>();
        var reflectionJob2 = reflectionProvider.GetRequiredService<SingletonJobService>();
        var sourceGenJob1 = sourceGenProvider.GetRequiredService<SingletonJobService>();
        var sourceGenJob2 = sourceGenProvider.GetRequiredService<SingletonJobService>();

        // Assert - both should be transient (different instances)
        Assert.NotSame(reflectionJob1, reflectionJob2);
        Assert.NotSame(sourceGenJob1, sourceGenJob2);
    }

    [Fact]
    public void Reflection_UsingOnlyAsTransient_DoesNotAffectOtherTypes()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        // Act - RegularSingletonService doesn't implement ITestJob
        var service1 = serviceProvider.GetRequiredService<RegularSingletonService>();
        var service2 = serviceProvider.GetRequiredService<RegularSingletonService>();

        // Assert - should be SAME instance (still singleton)
        Assert.Same(service1, service2);
    }

    [Fact]
    public void SourceGen_UsingOnlyAsTransient_DoesNotAffectOtherTypes()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        // Act - RegularSingletonService doesn't implement ITestJob
        var service1 = serviceProvider.GetRequiredService<RegularSingletonService>();
        var service2 = serviceProvider.GetRequiredService<RegularSingletonService>();

        // Assert - should be SAME instance (still singleton)
        Assert.Same(service1, service2);
    }

    [Fact]
    public void Parity_UsingOnlyAsTransient_BothPreserveOtherLifetimes()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>())
            .BuildServiceProvider();

        // Act
        var reflectionService1 = reflectionProvider.GetRequiredService<RegularSingletonService>();
        var reflectionService2 = reflectionProvider.GetRequiredService<RegularSingletonService>();
        var sourceGenService1 = sourceGenProvider.GetRequiredService<RegularSingletonService>();
        var sourceGenService2 = sourceGenProvider.GetRequiredService<RegularSingletonService>();

        // Assert - both should remain singleton (same instance)
        Assert.Same(reflectionService1, reflectionService2);
        Assert.Same(sourceGenService1, sourceGenService2);
    }

    [Fact]
    public void Parity_UsingOnlyAsTransient_WithPredicate_BothWork()
    {
        // Arrange - only override types with "Singleton" in name
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingTypeFilterer(new ReflectionTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>(t => t.Name.Contains("Singleton")))
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingTypeFilterer(new GeneratedTypeFilterer()
                .UsingOnlyAsTransient<ITestJob>(t => t.Name.Contains("Singleton")))
            .BuildServiceProvider();

        // Act - SingletonJobService matches predicate
        var reflectionJob1 = reflectionProvider.GetRequiredService<SingletonJobService>();
        var reflectionJob2 = reflectionProvider.GetRequiredService<SingletonJobService>();
        var sourceGenJob1 = sourceGenProvider.GetRequiredService<SingletonJobService>();
        var sourceGenJob2 = sourceGenProvider.GetRequiredService<SingletonJobService>();

        // Assert - should be transient (different instances)
        Assert.NotSame(reflectionJob1, reflectionJob2);
        Assert.NotSame(sourceGenJob1, sourceGenJob2);
    }
}
