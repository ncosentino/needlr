using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Reflection;

/// <summary>
/// Tests that verify lifetime attributes work correctly with reflection-based registration.
/// </summary>
public sealed class LifetimeAttributeReflectionTests
{
    [Fact]
    public void ScopedAttribute_RegistersAsScoped()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Assert - Scoped services return different instances from different scopes
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1a = scope1.ServiceProvider.GetRequiredService<ReflectionScopedService>();
        var instance1b = scope1.ServiceProvider.GetRequiredService<ReflectionScopedService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<ReflectionScopedService>();

        Assert.Same(instance1a, instance1b); // Same within scope
        Assert.NotSame(instance1a, instance2); // Different across scopes
    }

    [Fact]
    public void TransientAttribute_RegistersAsTransient()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Assert - Transient services return different instances each time
        var instance1 = provider.GetRequiredService<ReflectionTransientService>();
        var instance2 = provider.GetRequiredService<ReflectionTransientService>();

        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void SingletonAttribute_RegistersAsSingleton()
    {
        // Arrange & Act
        var provider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        // Assert - Singleton services return same instance
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<ReflectionExplicitSingletonService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<ReflectionExplicitSingletonService>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void ReflectionFilterer_ScopedAttribute_IdentifiesCorrectly()
    {
        var filterer = new ReflectionTypeFilterer();

        Assert.True(filterer.IsInjectableScopedType(typeof(ReflectionScopedService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(ReflectionScopedService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(ReflectionScopedService)));
    }

    [Fact]
    public void ReflectionFilterer_TransientAttribute_IdentifiesCorrectly()
    {
        var filterer = new ReflectionTypeFilterer();

        Assert.True(filterer.IsInjectableTransientType(typeof(ReflectionTransientService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(ReflectionTransientService)));
        Assert.False(filterer.IsInjectableScopedType(typeof(ReflectionTransientService)));
    }

    [Fact]
    public void ReflectionFilterer_SingletonAttribute_IdentifiesCorrectly()
    {
        var filterer = new ReflectionTypeFilterer();

        Assert.True(filterer.IsInjectableSingletonType(typeof(ReflectionExplicitSingletonService)));
        Assert.False(filterer.IsInjectableScopedType(typeof(ReflectionExplicitSingletonService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(ReflectionExplicitSingletonService)));
    }

    [Fact]
    public void ReflectionFilterer_NoAttribute_DefaultsToSingleton()
    {
        var filterer = new ReflectionTypeFilterer();

        // No lifetime attribute means Singleton by default
        Assert.True(filterer.IsInjectableSingletonType(typeof(ReflectionDefaultLifetimeService)));
        Assert.False(filterer.IsInjectableScopedType(typeof(ReflectionDefaultLifetimeService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(ReflectionDefaultLifetimeService)));
    }
}

// Test classes for reflection tests
public interface IReflectionScopedService { }

[Scoped]
public sealed class ReflectionScopedService : IReflectionScopedService { }

public interface IReflectionTransientService { }

[Transient]
public sealed class ReflectionTransientService : IReflectionTransientService { }

public interface IReflectionExplicitSingletonService { }

[Singleton]
public sealed class ReflectionExplicitSingletonService : IReflectionExplicitSingletonService { }

public interface IReflectionDefaultLifetimeService { }

public sealed class ReflectionDefaultLifetimeService : IReflectionDefaultLifetimeService { }
