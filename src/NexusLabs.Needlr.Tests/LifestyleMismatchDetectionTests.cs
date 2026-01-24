using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for lifestyle mismatch detection (captive dependency detection).
/// A lifestyle mismatch occurs when a longer-lived service depends on a shorter-lived service.
/// </summary>
public sealed class LifestyleMismatchDetectionTests
{
    [Fact]
    public void DetectLifestyleMismatches_SingletonDependsOnScoped_ReturnsMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddSingleton<ISingletonService, SingletonWithScopedDependency>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        var mismatch = Assert.Single(mismatches);
        Assert.Equal(typeof(ISingletonService), mismatch.ConsumerServiceType);
        Assert.Equal(typeof(IScopedDependency), mismatch.DependencyServiceType);
        Assert.Equal(ServiceLifetime.Singleton, mismatch.ConsumerLifetime);
        Assert.Equal(ServiceLifetime.Scoped, mismatch.DependencyLifetime);
    }

    [Fact]
    public void DetectLifestyleMismatches_SingletonDependsOnTransient_ReturnsMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITransientDependency, TransientDependency>();
        services.AddSingleton<ISingletonService, SingletonWithTransientDependency>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        var mismatch = Assert.Single(mismatches);
        Assert.Equal(typeof(ISingletonService), mismatch.ConsumerServiceType);
        Assert.Equal(typeof(ITransientDependency), mismatch.DependencyServiceType);
    }

    [Fact]
    public void DetectLifestyleMismatches_ScopedDependsOnTransient_ReturnsMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITransientDependency, TransientDependency>();
        services.AddScoped<IScopedService, ScopedWithTransientDependency>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        var mismatch = Assert.Single(mismatches);
        Assert.Equal(typeof(IScopedService), mismatch.ConsumerServiceType);
        Assert.Equal(typeof(ITransientDependency), mismatch.DependencyServiceType);
    }

    [Fact]
    public void DetectLifestyleMismatches_SingletonDependsOnSingleton_NoMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonDependency, SingletonDependency>();
        services.AddSingleton<ISingletonService, SingletonWithSingletonDependency>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public void DetectLifestyleMismatches_ScopedDependsOnSingleton_NoMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonDependency, SingletonDependency>();
        services.AddScoped<IScopedService, ScopedWithSingletonDependency>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public void DetectLifestyleMismatches_ScopedDependsOnScoped_NoMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddScoped<IScopedService, ScopedWithScopedDependency>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public void DetectLifestyleMismatches_TransientDependsOnAnything_NoMismatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonDependency, SingletonDependency>();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddTransient<ITransientService, TransientWithMixedDependencies>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        Assert.Empty(mismatches);
    }

    [Fact]
    public void DetectLifestyleMismatches_MultipleMismatches_ReturnsAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITransientDependency, TransientDependency>();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddSingleton<ISingletonService, SingletonWithMultipleBadDependencies>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert
        Assert.Equal(2, mismatches.Count);
    }

    [Fact]
    public void DetectLifestyleMismatches_ChainedMismatches_DetectsAll()
    {
        // Arrange - A (Singleton) -> B (Scoped) -> C (Transient)
        var services = new ServiceCollection();
        services.AddTransient<ITransientDependency, TransientDependency>();
        services.AddScoped<IScopedDependency, ScopedWithTransientDependencyImpl>();
        services.AddSingleton<ISingletonService, SingletonWithScopedDependency>();

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert - should detect both: Singleton->Scoped and Scoped->Transient
        Assert.Equal(2, mismatches.Count);
    }

    [Fact]
    public void DetectLifestyleMismatches_FactoryRegistration_IsSkipped()
    {
        // Arrange - Factory registrations can't be analyzed for dependencies
        var services = new ServiceCollection();
        services.AddScoped<IScopedDependency, ScopedDependency>();
        services.AddSingleton<ISingletonService>(sp => new SingletonWithScopedDependency(
            sp.GetRequiredService<IScopedDependency>()));

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert - Can't detect mismatch in factory, so empty
        Assert.Empty(mismatches);
    }

    [Fact]
    public void DetectLifestyleMismatches_UnregisteredDependency_IsSkipped()
    {
        // Arrange - Dependency not registered, can't determine its lifetime
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, SingletonWithScopedDependency>();
        // IScopedDependency is NOT registered

        // Act
        var mismatches = services.DetectLifestyleMismatches();

        // Assert - Can't determine lifetime of unregistered service
        Assert.Empty(mismatches);
    }

    [Fact]
    public void DetectLifestyleMismatches_NullServiceCollection_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services!.DetectLifestyleMismatches());
    }

    [Fact]
    public void LifestyleMismatch_ToDetailedString_IncludesAllInfo()
    {
        // Arrange
        var mismatch = new LifestyleMismatch(
            ConsumerServiceType: typeof(ISingletonService),
            ConsumerImplementationType: typeof(SingletonWithScopedDependency),
            ConsumerLifetime: ServiceLifetime.Singleton,
            DependencyServiceType: typeof(IScopedDependency),
            DependencyLifetime: ServiceLifetime.Scoped);

        // Act
        var result = mismatch.ToDetailedString();

        // Assert
        Assert.Contains("ISingletonService", result);
        Assert.Contains("IScopedDependency", result);
        Assert.Contains("Singleton", result);
        Assert.Contains("Scoped", result);
    }

    // Test interfaces and classes
    public interface ISingletonService { }
    public interface IScopedService { }
    public interface ITransientService { }
    public interface ISingletonDependency { }
    public interface IScopedDependency { }
    public interface ITransientDependency { }

    public class SingletonDependency : ISingletonDependency { }
    public class ScopedDependency : IScopedDependency { }
    public class TransientDependency : ITransientDependency { }

    public class SingletonWithScopedDependency : ISingletonService
    {
        public SingletonWithScopedDependency(IScopedDependency dependency) { }
    }

    public class SingletonWithTransientDependency : ISingletonService
    {
        public SingletonWithTransientDependency(ITransientDependency dependency) { }
    }

    public class SingletonWithSingletonDependency : ISingletonService
    {
        public SingletonWithSingletonDependency(ISingletonDependency dependency) { }
    }

    public class SingletonWithMultipleBadDependencies : ISingletonService
    {
        public SingletonWithMultipleBadDependencies(
            IScopedDependency scoped,
            ITransientDependency transient) { }
    }

    public class ScopedWithTransientDependency : IScopedService
    {
        public ScopedWithTransientDependency(ITransientDependency dependency) { }
    }

    public class ScopedWithSingletonDependency : IScopedService
    {
        public ScopedWithSingletonDependency(ISingletonDependency dependency) { }
    }

    public class ScopedWithScopedDependency : IScopedService
    {
        public ScopedWithScopedDependency(IScopedDependency dependency) { }
    }

    public class ScopedWithTransientDependencyImpl : IScopedDependency
    {
        public ScopedWithTransientDependencyImpl(ITransientDependency dependency) { }
    }

    public class TransientWithMixedDependencies : ITransientService
    {
        public TransientWithMixedDependencies(
            ISingletonDependency singleton,
            IScopedDependency scoped) { }
    }
}
