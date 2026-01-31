using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for AddDecorator with different lifetimes and implementation patterns.
/// </summary>
public sealed class ServiceCollectionExtensionsDecoratorTests
{
    [Fact]
    public void AddDecorator_WithScopedService_PreservesLifetime()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDecoratorService, OriginalService>();
        services.AddDecorator<IDecoratorService, ServiceDecorator>();

        var descriptor = services.Single(d => d.ServiceType == typeof(IDecoratorService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1a = scope1.ServiceProvider.GetRequiredService<IDecoratorService>();
        var instance1b = scope1.ServiceProvider.GetRequiredService<IDecoratorService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<IDecoratorService>();

        Assert.Same(instance1a, instance1b); // Same within scope
        Assert.NotSame(instance1a, instance2); // Different across scopes
        Assert.IsType<ServiceDecorator>(instance1a);
    }

    [Fact]
    public void AddDecorator_WithTransientService_PreservesLifetime()
    {
        var services = new ServiceCollection();
        services.AddTransient<IDecoratorService, OriginalService>();
        services.AddDecorator<IDecoratorService, ServiceDecorator>();

        var descriptor = services.Single(d => d.ServiceType == typeof(IDecoratorService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IDecoratorService>();
        var instance2 = provider.GetRequiredService<IDecoratorService>();

        Assert.NotSame(instance1, instance2); // Different instances for transient
        Assert.IsType<ServiceDecorator>(instance1);
        Assert.IsType<ServiceDecorator>(instance2);
    }

    [Fact]
    public void AddDecorator_WithSingletonService_PreservesLifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDecoratorService, OriginalService>();
        services.AddDecorator<IDecoratorService, ServiceDecorator>();

        var descriptor = services.Single(d => d.ServiceType == typeof(IDecoratorService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IDecoratorService>();
        var instance2 = provider.GetRequiredService<IDecoratorService>();

        Assert.Same(instance1, instance2); // Same instance for singleton
        Assert.IsType<ServiceDecorator>(instance1);
    }

    [Fact]
    public void AddDecorator_WithImplementationFactory_DecoratesCorrectly()
    {
        var factoryCallCount = 0;
        var services = new ServiceCollection();
        services.AddTransient<IDecoratorService>(_ =>
        {
            factoryCallCount++;
            return new OriginalService();
        });
        services.AddDecorator<IDecoratorService, ServiceDecorator>();

        using var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IDecoratorService>();

        Assert.IsType<ServiceDecorator>(instance);
        Assert.Equal("Decorated: Original", instance.GetValue());
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void AddDecorator_WithImplementationInstance_DecoratesCorrectly()
    {
        var originalInstance = new OriginalService();
        var services = new ServiceCollection();
        services.AddSingleton<IDecoratorService>(originalInstance);
        services.AddDecorator<IDecoratorService, ServiceDecorator>();

        using var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IDecoratorService>();

        Assert.IsType<ServiceDecorator>(instance);
        var decorator = (ServiceDecorator)instance;
        Assert.Same(originalInstance, decorator.Inner);
    }

    [Fact]
    public void AddDecorator_WithMultipleRegistrations_DecoratesAllRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDecoratorService, OriginalService>();
        services.AddSingleton<IDecoratorService, AlternativeService>();
        services.AddDecorator<IDecoratorService, ServiceDecorator>();

        using var provider = services.BuildServiceProvider();
        var instances = provider.GetServices<IDecoratorService>().ToList();

        // Both registrations should be decorated
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<ServiceDecorator>(instance));
        
        // Each should wrap a different inner service
        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Decorated: Alternative", values);
        Assert.Contains("Decorated: Original", values);
    }

    [Fact]
    public void AddDecorator_WithMultipleRegistrations_ChainedDecorators_DecoratesAll()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDecoratorService, OriginalService>();
        services.AddSingleton<IDecoratorService, AlternativeService>();
        services.AddDecorator<IDecoratorService, ServiceDecorator>();
        services.AddDecorator<IDecoratorService, SecondDecorator>();

        using var provider = services.BuildServiceProvider();
        var instances = provider.GetServices<IDecoratorService>().ToList();

        // Both registrations should be decorated with both decorators
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<SecondDecorator>(instance));
        
        var values = instances.Select(i => i.GetValue()).OrderBy(v => v).ToList();
        Assert.Contains("Second: Decorated: Alternative", values);
        Assert.Contains("Second: Decorated: Original", values);
    }

    [Fact]
    public void AddDecorator_WithDifferentLifetimes_DecoratesAllAndPreservesLifetimes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDecoratorService, OriginalService>();
        services.AddScoped<IDecoratorService, AlternativeService>();
        services.AddDecorator<IDecoratorService, ServiceDecorator>();

        // Should have two registrations with different lifetimes
        var descriptors = services.Where(d => d.ServiceType == typeof(IDecoratorService)).ToList();
        Assert.Equal(2, descriptors.Count);
        Assert.Contains(descriptors, d => d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(descriptors, d => d.Lifetime == ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        
        var instances = scope.ServiceProvider.GetServices<IDecoratorService>().ToList();
        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.IsType<ServiceDecorator>(instance));
    }

    [Fact]
    public void AddDecorator_ChainedDecorators_WorksCorrectly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDecoratorService, OriginalService>();
        services.AddDecorator<IDecoratorService, ServiceDecorator>();
        services.AddDecorator<IDecoratorService, SecondDecorator>();

        using var provider = services.BuildServiceProvider();
        var instance = provider.GetRequiredService<IDecoratorService>();

        Assert.IsType<SecondDecorator>(instance);
        Assert.Equal("Second: Decorated: Original", instance.GetValue());
    }

    public interface IDecoratorService
    {
        string GetValue();
    }

    [DoNotAutoRegister]
    public sealed class OriginalService : IDecoratorService
    {
        public string GetValue() => "Original";
    }

    [DoNotAutoRegister]
    public sealed class AlternativeService : IDecoratorService
    {
        public string GetValue() => "Alternative";
    }

    [DoNotAutoRegister]
    public sealed class ServiceDecorator : IDecoratorService
    {
        public IDecoratorService Inner { get; }

        public ServiceDecorator(IDecoratorService inner)
        {
            Inner = inner;
        }

        public string GetValue() => $"Decorated: {Inner.GetValue()}";
    }

    [DoNotAutoRegister]
    public sealed class SecondDecorator : IDecoratorService
    {
        private readonly IDecoratorService _inner;

        public SecondDecorator(IDecoratorService inner)
        {
            _inner = inner;
        }

        public string GetValue() => $"Second: {_inner.GetValue()}";
    }
}
