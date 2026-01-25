using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify reflection and source-gen behave identically for lifetime attributes.
/// Each test compares both paths in the same scenario.
/// </summary>
public sealed class LifetimeAttributeParityTests
{
    [Fact]
    public void Parity_ScopedAttribute_BothPathsRegisterAsScoped()
    {
        // Arrange - Build both providers
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Act & Assert - Both return different instances across scopes (scoped behavior)
        using var reflectionScope1 = reflectionProvider.CreateScope();
        using var reflectionScope2 = reflectionProvider.CreateScope();
        using var sourceGenScope1 = sourceGenProvider.CreateScope();
        using var sourceGenScope2 = sourceGenProvider.CreateScope();

        var reflectionInstance1 = reflectionScope1.ServiceProvider.GetRequiredService<ParityScopedService>();
        var reflectionInstance2 = reflectionScope2.ServiceProvider.GetRequiredService<ParityScopedService>();
        var sourceGenInstance1 = sourceGenScope1.ServiceProvider.GetRequiredService<ParityScopedService>();
        var sourceGenInstance2 = sourceGenScope2.ServiceProvider.GetRequiredService<ParityScopedService>();

        // Both paths should show scoped behavior
        Assert.NotSame(reflectionInstance1, reflectionInstance2);
        Assert.NotSame(sourceGenInstance1, sourceGenInstance2);
    }

    [Fact]
    public void Parity_TransientAttribute_BothPathsRegisterAsTransient()
    {
        // Arrange - Build both providers
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Act - Get instances twice from each
        var reflectionInstance1 = reflectionProvider.GetRequiredService<ParityTransientService>();
        var reflectionInstance2 = reflectionProvider.GetRequiredService<ParityTransientService>();
        var sourceGenInstance1 = sourceGenProvider.GetRequiredService<ParityTransientService>();
        var sourceGenInstance2 = sourceGenProvider.GetRequiredService<ParityTransientService>();

        // Assert - Both paths should show transient behavior (different instances)
        Assert.NotSame(reflectionInstance1, reflectionInstance2);
        Assert.NotSame(sourceGenInstance1, sourceGenInstance2);
    }

    [Fact]
    public void Parity_SingletonAttribute_BothPathsRegisterAsSingleton()
    {
        // Arrange - Build both providers
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Act & Assert - Both return same instance across scopes (singleton behavior)
        using var reflectionScope1 = reflectionProvider.CreateScope();
        using var reflectionScope2 = reflectionProvider.CreateScope();
        using var sourceGenScope1 = sourceGenProvider.CreateScope();
        using var sourceGenScope2 = sourceGenProvider.CreateScope();

        var reflectionInstance1 = reflectionScope1.ServiceProvider.GetRequiredService<ParitySingletonService>();
        var reflectionInstance2 = reflectionScope2.ServiceProvider.GetRequiredService<ParitySingletonService>();
        var sourceGenInstance1 = sourceGenScope1.ServiceProvider.GetRequiredService<ParitySingletonService>();
        var sourceGenInstance2 = sourceGenScope2.ServiceProvider.GetRequiredService<ParitySingletonService>();

        // Both paths should show singleton behavior
        Assert.Same(reflectionInstance1, reflectionInstance2);
        Assert.Same(sourceGenInstance1, sourceGenInstance2);
    }

    [Fact]
    public void Parity_NoAttribute_BothPathsDefaultToSingleton()
    {
        // Arrange - Build both providers
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Act & Assert - Both return same instance across scopes (singleton behavior by default)
        using var reflectionScope1 = reflectionProvider.CreateScope();
        using var reflectionScope2 = reflectionProvider.CreateScope();
        using var sourceGenScope1 = sourceGenProvider.CreateScope();
        using var sourceGenScope2 = sourceGenProvider.CreateScope();

        var reflectionInstance1 = reflectionScope1.ServiceProvider.GetRequiredService<ParityDefaultService>();
        var reflectionInstance2 = reflectionScope2.ServiceProvider.GetRequiredService<ParityDefaultService>();
        var sourceGenInstance1 = sourceGenScope1.ServiceProvider.GetRequiredService<ParityDefaultService>();
        var sourceGenInstance2 = sourceGenScope2.ServiceProvider.GetRequiredService<ParityDefaultService>();

        // Both paths should show singleton behavior (the default)
        Assert.Same(reflectionInstance1, reflectionInstance2);
        Assert.Same(sourceGenInstance1, sourceGenInstance2);
    }

    [Fact]
    public void Parity_TypeFilterers_AgreeonLifetimes()
    {
        // Arrange
        var reflectionFilterer = new ReflectionTypeFilterer();
        var generatedFilterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes);

        // Assert - Both filterers agree on lifetime for each type
        Assert.Equal(
            reflectionFilterer.IsInjectableScopedType(typeof(ParityScopedService)),
            generatedFilterer.IsInjectableScopedType(typeof(ParityScopedService)));

        Assert.Equal(
            reflectionFilterer.IsInjectableTransientType(typeof(ParityTransientService)),
            generatedFilterer.IsInjectableTransientType(typeof(ParityTransientService)));

        Assert.Equal(
            reflectionFilterer.IsInjectableSingletonType(typeof(ParitySingletonService)),
            generatedFilterer.IsInjectableSingletonType(typeof(ParitySingletonService)));

        Assert.Equal(
            reflectionFilterer.IsInjectableSingletonType(typeof(ParityDefaultService)),
            generatedFilterer.IsInjectableSingletonType(typeof(ParityDefaultService)));
    }
}

// Test classes for parity tests - shared by both reflection and source-gen
public interface IParityScopedService { }

[Scoped]
public sealed class ParityScopedService : IParityScopedService { }

public interface IParityTransientService { }

[Transient]
public sealed class ParityTransientService : IParityTransientService { }

public interface IParitySingletonService { }

[Singleton]
public sealed class ParitySingletonService : IParitySingletonService { }

public interface IParityDefaultService { }

public sealed class ParityDefaultService : IParityDefaultService { }
