using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Parity tests for the non-generic <c>AddDecorator(IServiceCollection, Type, Type)</c> overload,
/// covering every descriptor shape, lifetime, and failure mode supported by the generic overload.
/// </summary>
public sealed class ServiceCollectionExtensionsNonGenericDecoratorTests
{
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddDecorator_WithImplementationType_PreservesLifetimeAndDecorates(ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(new ServiceDescriptor(
            typeof(INonGenericDecoratorService),
            typeof(NonGenericOriginalService),
            lifetime));
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(INonGenericDecoratorService));
        Assert.Equal(lifetime, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<INonGenericDecoratorService>();
        var second = scope.ServiceProvider.GetRequiredService<INonGenericDecoratorService>();

        var decorator = Assert.IsType<NonGenericDecorator>(first);
        Assert.IsType<NonGenericOriginalService>(decorator.Inner);
        Assert.Equal("Decorated: Original", first.GetValue());

        if (lifetime == ServiceLifetime.Transient)
        {
            Assert.NotSame(first, second);
        }
        else
        {
            Assert.Same(first, second);
        }
    }

    [Fact]
    public void AddDecorator_WithScopedService_ResolvesDistinctInstancesPerScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<INonGenericDecoratorService, NonGenericOriginalService>();
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));

        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<INonGenericDecoratorService>();
        var second = secondScope.ServiceProvider.GetRequiredService<INonGenericDecoratorService>();

        Assert.NotSame(first, second);
        Assert.IsType<NonGenericDecorator>(first);
        Assert.IsType<NonGenericDecorator>(second);
    }

    [Fact]
    public void AddDecorator_WithImplementationFactory_InvokesFactoryOncePerActivation()
    {
        var factoryCallCount = 0;
        var services = new ServiceCollection();
        services.AddTransient<INonGenericDecoratorService>(_ =>
        {
            factoryCallCount++;
            return new NonGenericOriginalService();
        });
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<INonGenericDecoratorService>();
        Assert.Equal(1, factoryCallCount);
        Assert.Equal("Decorated: Original", first.GetValue());

        provider.GetRequiredService<INonGenericDecoratorService>();
        Assert.Equal(2, factoryCallCount);
    }

    [Fact]
    public void AddDecorator_WithSingletonImplementationFactory_InvokesFactoryOnce()
    {
        var factoryCallCount = 0;
        var services = new ServiceCollection();
        services.AddSingleton<INonGenericDecoratorService>(_ =>
        {
            factoryCallCount++;
            return new NonGenericOriginalService();
        });
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<INonGenericDecoratorService>();
        var second = provider.GetRequiredService<INonGenericDecoratorService>();

        Assert.Same(first, second);
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void AddDecorator_WithImplementationInstance_WrapsThatExactInstance()
    {
        var originalInstance = new NonGenericOriginalService();
        var services = new ServiceCollection();
        services.AddSingleton<INonGenericDecoratorService>(originalInstance);
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<INonGenericDecoratorService>();

        var decorator = Assert.IsType<NonGenericDecorator>(resolved);
        Assert.Same(originalInstance, decorator.Inner);
    }

    [Fact]
    public void AddDecorator_WithMultipleRegistrations_PreservesOrderAndLifetimes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INonGenericDecoratorService, NonGenericOriginalService>();
        services.AddScoped<INonGenericDecoratorService, NonGenericAlternativeService>();
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));

        var descriptors = services
            .Where(d => d.ServiceType == typeof(INonGenericDecoratorService))
            .ToList();
        Assert.Equal(2, descriptors.Count);
        Assert.Equal(ServiceLifetime.Singleton, descriptors[0].Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, descriptors[1].Lifetime);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetServices<INonGenericDecoratorService>().ToList();
        Assert.Equal(2, resolved.Count);
        Assert.Equal("Decorated: Original", resolved[0].GetValue());
        Assert.Equal("Decorated: Alternative", resolved[1].GetValue());
    }

    [Fact]
    public void AddDecorator_ChainedDecorators_PreserveInnerIdentityAndOrder()
    {
        var originalInstance = new NonGenericOriginalService();
        var services = new ServiceCollection();
        services.AddSingleton<INonGenericDecoratorService>(originalInstance);
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericSecondDecorator));

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<INonGenericDecoratorService>();

        var outer = Assert.IsType<NonGenericSecondDecorator>(resolved);
        var inner = Assert.IsType<NonGenericDecorator>(outer.Inner);
        Assert.Same(originalInstance, inner.Inner);
        Assert.Equal("Second: Decorated: Original", resolved.GetValue());
    }

    [Fact]
    public void AddDecorator_WithoutExistingRegistration_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator)));

        Assert.Contains(nameof(INonGenericDecoratorService), exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void AddDecorator_WithNullArguments_Throws()
    {
        IServiceCollection nullServices = null!;
        Assert.Throws<ArgumentNullException>(
            () => nullServices.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator)));

        var services = new ServiceCollection();
        services.AddSingleton<INonGenericDecoratorService, NonGenericOriginalService>();

        Assert.Throws<ArgumentNullException>(
            () => services.AddDecorator(null!, typeof(NonGenericDecorator)));
        Assert.Throws<ArgumentNullException>(
            () => services.AddDecorator(typeof(INonGenericDecoratorService), null!));
    }

    [Fact]
    public void AddDecorator_WithUnsupportedLifetime_ThrowsAndLeavesCollectionUnchanged()
    {
        IServiceCollection services = new ServiceCollection();
        var originalDescriptor = new ServiceDescriptor(
            typeof(INonGenericDecoratorService),
            _ => new NonGenericOriginalService(),
            (ServiceLifetime)int.MaxValue);
        services.Add(originalDescriptor);

        Assert.Throws<InvalidOperationException>(
            () => services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator)));

        Assert.Same(originalDescriptor, Assert.Single(services));
    }

    [Fact]
    public void AddDecorator_WithIncompatibleDecoratorType_ThrowsOnResolution()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INonGenericDecoratorService, NonGenericOriginalService>();
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(UnrelatedDecorator));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidCastException>(
            () => provider.GetRequiredService<INonGenericDecoratorService>());
    }

    [Fact]
    public void AddDecorator_WithDecoratorMissingInnerParameter_ThrowsOnResolution()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INonGenericDecoratorService, NonGenericOriginalService>();
        services.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecoratorWithoutInner));

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<INonGenericDecoratorService>());
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddDecorator_GenericAndNonGenericOverloads_ProduceEquivalentRegistrations(ServiceLifetime lifetime)
    {
        IServiceCollection genericServices = new ServiceCollection();
        genericServices.Add(new ServiceDescriptor(
            typeof(INonGenericDecoratorService),
            typeof(NonGenericOriginalService),
            lifetime));
        genericServices.AddDecorator<INonGenericDecoratorService, NonGenericDecorator>();

        IServiceCollection nonGenericServices = new ServiceCollection();
        nonGenericServices.Add(new ServiceDescriptor(
            typeof(INonGenericDecoratorService),
            typeof(NonGenericOriginalService),
            lifetime));
        nonGenericServices.AddDecorator(typeof(INonGenericDecoratorService), typeof(NonGenericDecorator));

        var genericDescriptor = Assert.Single(genericServices);
        var nonGenericDescriptor = Assert.Single(nonGenericServices);

        Assert.Equal(genericDescriptor.ServiceType, nonGenericDescriptor.ServiceType);
        Assert.Equal(genericDescriptor.Lifetime, nonGenericDescriptor.Lifetime);
        Assert.NotNull(genericDescriptor.ImplementationFactory);
        Assert.NotNull(nonGenericDescriptor.ImplementationFactory);

        using var genericProvider = genericServices.BuildServiceProvider();
        using var nonGenericProvider = nonGenericServices.BuildServiceProvider();
        using var genericScope = genericProvider.CreateScope();
        using var nonGenericScope = nonGenericProvider.CreateScope();

        var fromGeneric = genericScope.ServiceProvider.GetRequiredService<INonGenericDecoratorService>();
        var fromNonGeneric = nonGenericScope.ServiceProvider.GetRequiredService<INonGenericDecoratorService>();

        Assert.IsType<NonGenericDecorator>(fromGeneric);
        Assert.IsType<NonGenericDecorator>(fromNonGeneric);
        Assert.Equal(fromGeneric.GetValue(), fromNonGeneric.GetValue());
    }

    [Fact]
    public void AddDecorator_GenericOverload_WithoutExistingRegistration_ThrowsLikeNonGeneric()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddDecorator<INonGenericDecoratorService, NonGenericDecorator>());

        Assert.Contains(nameof(INonGenericDecoratorService), exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void AddDecorator_GenericOverload_WithUnsupportedLifetime_ThrowsLikeNonGeneric()
    {
        IServiceCollection services = new ServiceCollection();
        var originalDescriptor = new ServiceDescriptor(
            typeof(INonGenericDecoratorService),
            _ => new NonGenericOriginalService(),
            (ServiceLifetime)int.MaxValue);
        services.Add(originalDescriptor);

        Assert.Throws<InvalidOperationException>(
            () => services.AddDecorator<INonGenericDecoratorService, NonGenericDecorator>());

        Assert.Same(originalDescriptor, Assert.Single(services));
    }

    [Fact]
    public void AddDecorator_WithConcreteServiceType_DecoratesClassRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NonGenericOriginalService>();
        services.AddDecorator(typeof(NonGenericOriginalService), typeof(NonGenericSubclassDecorator));

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<NonGenericOriginalService>();

        var decorator = Assert.IsType<NonGenericSubclassDecorator>(resolved);
        Assert.IsType<NonGenericOriginalService>(decorator.Inner);
        Assert.Equal("Subclass: Original", resolved.GetValue());
    }

    public interface INonGenericDecoratorService
    {
        string GetValue();
    }

    [DoNotAutoRegister]
    public class NonGenericOriginalService : INonGenericDecoratorService
    {
        public virtual string GetValue() => "Original";
    }

    [DoNotAutoRegister]
    public sealed class NonGenericAlternativeService : INonGenericDecoratorService
    {
        public string GetValue() => "Alternative";
    }

    [DoNotAutoRegister]
    public sealed class NonGenericDecorator : INonGenericDecoratorService
    {
        public NonGenericDecorator(INonGenericDecoratorService inner)
        {
            Inner = inner;
        }

        public INonGenericDecoratorService Inner { get; }

        public string GetValue() => $"Decorated: {Inner.GetValue()}";
    }

    [DoNotAutoRegister]
    public sealed class NonGenericSecondDecorator : INonGenericDecoratorService
    {
        public NonGenericSecondDecorator(INonGenericDecoratorService inner)
        {
            Inner = inner;
        }

        public INonGenericDecoratorService Inner { get; }

        public string GetValue() => $"Second: {Inner.GetValue()}";
    }

    [DoNotAutoRegister]
    public sealed class NonGenericSubclassDecorator : NonGenericOriginalService
    {
        public NonGenericSubclassDecorator(NonGenericOriginalService inner)
        {
            Inner = inner;
        }

        public NonGenericOriginalService Inner { get; }

        public override string GetValue() => $"Subclass: {Inner.GetValue()}";
    }

    [DoNotAutoRegister]
    public sealed class NonGenericDecoratorWithoutInner : INonGenericDecoratorService
    {
        public NonGenericDecoratorWithoutInner(IDisposable required)
        {
            Required = required;
        }

        public IDisposable Required { get; }

        public string GetValue() => "No inner";
    }

    [DoNotAutoRegister]
    public sealed class UnrelatedDecorator
    {
        public UnrelatedDecorator(INonGenericDecoratorService inner)
        {
            Inner = inner;
        }

        public INonGenericDecoratorService Inner { get; }
    }
}
