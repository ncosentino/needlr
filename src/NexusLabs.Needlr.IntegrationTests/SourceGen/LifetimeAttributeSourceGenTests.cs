using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Tests that verify lifetime attributes work correctly with source generation.
/// </summary>
public sealed class LifetimeAttributeSourceGenTests
{
    [Fact]
    public void ScopedAttribute_RegistersAsScoped()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Assert - Scoped services return different instances from different scopes
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1a = scope1.ServiceProvider.GetRequiredService<SourceGenScopedService>();
        var instance1b = scope1.ServiceProvider.GetRequiredService<SourceGenScopedService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<SourceGenScopedService>();

        Assert.Same(instance1a, instance1b); // Same within scope
        Assert.NotSame(instance1a, instance2); // Different across scopes
    }

    [Fact]
    public void TransientAttribute_RegistersAsTransient()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Assert - Transient services return different instances each time
        var instance1 = provider.GetRequiredService<SourceGenTransientService>();
        var instance2 = provider.GetRequiredService<SourceGenTransientService>();

        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void SingletonAttribute_RegistersAsSingleton()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Assert - Singleton services return same instance
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<SourceGenExplicitSingletonService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<SourceGenExplicitSingletonService>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void NoAttribute_DefaultsToSingleton()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        // Assert - Default (no attribute) services are Singleton
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<SourceGenDefaultLifetimeService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<SourceGenDefaultLifetimeService>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GeneratedMetadata_ScopedAttribute_ProducesCorrectLifetime()
    {
        // Verify the generator produces the correct lifetime metadata
        var injectableTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var scopedType = injectableTypes.FirstOrDefault(t => t.Type == typeof(SourceGenScopedService));

        Assert.NotNull(scopedType.Type);
        Assert.Equal(InjectableLifetime.Scoped, scopedType.Lifetime);
    }

    [Fact]
    public void GeneratedMetadata_TransientAttribute_ProducesCorrectLifetime()
    {
        // Verify the generator produces the correct lifetime metadata
        var injectableTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var transientType = injectableTypes.FirstOrDefault(t => t.Type == typeof(SourceGenTransientService));

        Assert.NotNull(transientType.Type);
        Assert.Equal(InjectableLifetime.Transient, transientType.Lifetime);
    }

    [Fact]
    public void GeneratedMetadata_SingletonAttribute_ProducesCorrectLifetime()
    {
        // Verify the generator produces the correct lifetime metadata
        var injectableTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var singletonType = injectableTypes.FirstOrDefault(t => t.Type == typeof(SourceGenExplicitSingletonService));

        Assert.NotNull(singletonType.Type);
        Assert.Equal(InjectableLifetime.Singleton, singletonType.Lifetime);
    }

    [Fact]
    public void GeneratedMetadata_NoAttribute_ProducesSingletonLifetime()
    {
        // Verify the generator produces Singleton lifetime when no attribute is present
        var injectableTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var defaultType = injectableTypes.FirstOrDefault(t => t.Type == typeof(SourceGenDefaultLifetimeService));

        Assert.NotNull(defaultType.Type);
        Assert.Equal(InjectableLifetime.Singleton, defaultType.Lifetime);
    }

    [Fact]
    public void GeneratedFilterer_ScopedAttribute_IdentifiesCorrectly()
    {
        var filterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes);

        Assert.True(filterer.IsInjectableScopedType(typeof(SourceGenScopedService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(SourceGenScopedService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(SourceGenScopedService)));
    }

    [Fact]
    public void GeneratedFilterer_TransientAttribute_IdentifiesCorrectly()
    {
        var filterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes);

        Assert.True(filterer.IsInjectableTransientType(typeof(SourceGenTransientService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(SourceGenTransientService)));
        Assert.False(filterer.IsInjectableScopedType(typeof(SourceGenTransientService)));
    }
}

// Test classes for source-gen tests
public interface ISourceGenScopedService { }

[Scoped]
public sealed class SourceGenScopedService : ISourceGenScopedService { }

public interface ISourceGenTransientService { }

[Transient]
public sealed class SourceGenTransientService : ISourceGenTransientService { }

public interface ISourceGenExplicitSingletonService { }

[Singleton]
public sealed class SourceGenExplicitSingletonService : ISourceGenExplicitSingletonService { }

public interface ISourceGenDefaultLifetimeService { }

public sealed class SourceGenDefaultLifetimeService : ISourceGenDefaultLifetimeService { }
