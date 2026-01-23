using NexusLabs.Needlr.Injection.SourceGen.TypeRegistrars;
using NexusLabs.Needlr.Generators;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeRegistrars;

public sealed class GeneratedTypeRegistrarTests
{
    [Fact]
    public void RegisterTypesFromAssemblies_WithTypeProvider_RegistersTypesCorrectly()
    {
        var services = new ServiceCollection();
        var typeInfo = new InjectableTypeInfo(
            typeof(TestService),
            [typeof(ITestService)],
            InjectableLifetime.Singleton,
            _ => new TestService());

        var registrar = new GeneratedTypeRegistrar(() => [typeInfo]);

        registrar.RegisterTypesFromAssemblies(services, new AllowAllTypeFilterer(), []);

        Assert.Contains(services, d => d.ServiceType == typeof(TestService));
        Assert.Contains(services, d => d.ServiceType == typeof(ITestService));
    }

    [Fact]
    public void RegisterTypesFromAssemblies_WithNullTypeProvider_HandlesGracefully()
    {
        var services = new ServiceCollection();
        var registrar = new GeneratedTypeRegistrar(null);

        // Should not throw - just returns empty
        registrar.RegisterTypesFromAssemblies(services, new AllowAllTypeFilterer(), []);

        Assert.Empty(services);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_TransientLifetime_RegistersAsTransient()
    {
        var services = new ServiceCollection();
        var typeInfo = new InjectableTypeInfo(
            typeof(TestService),
            [],
            InjectableLifetime.Transient,
            _ => new TestService());

        var registrar = new GeneratedTypeRegistrar(() => [typeInfo]);

        registrar.RegisterTypesFromAssemblies(services, new AllowAllTypeFilterer(), []);

        var descriptor = services.First(d => d.ServiceType == typeof(TestService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_ScopedLifetime_RegistersAsScoped()
    {
        var services = new ServiceCollection();
        var typeInfo = new InjectableTypeInfo(
            typeof(TestService),
            [],
            InjectableLifetime.Scoped,
            _ => new TestService());

        var registrar = new GeneratedTypeRegistrar(() => [typeInfo]);

        registrar.RegisterTypesFromAssemblies(services, new AllowAllTypeFilterer(), []);

        var descriptor = services.First(d => d.ServiceType == typeof(TestService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_TypeWithoutLifetime_IsSkipped()
    {
        var services = new ServiceCollection();
        var typeInfo = new InjectableTypeInfo(
            typeof(TestService),
            [],
            null, // No lifetime
            _ => new TestService());

        var registrar = new GeneratedTypeRegistrar(() => [typeInfo]);

        registrar.RegisterTypesFromAssemblies(services, new AllowAllTypeFilterer(), []);

        Assert.Empty(services);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_TypeWithNullFactory_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var typeInfo = new InjectableTypeInfo(
            typeof(TestService),
            [],
            InjectableLifetime.Singleton,
            null!); // Null factory

        var registrar = new GeneratedTypeRegistrar(() => [typeInfo]);

        Assert.Throws<InvalidOperationException>(() =>
            registrar.RegisterTypesFromAssemblies(services, new AllowAllTypeFilterer(), []));
    }

    [Fact]
    public void RegisterTypesFromAssemblies_SingletonWithInterface_ResolvesToSameInstance()
    {
        var services = new ServiceCollection();
        var typeInfo = new InjectableTypeInfo(
            typeof(TestService),
            [typeof(ITestService)],
            InjectableLifetime.Singleton,
            _ => new TestService());

        var registrar = new GeneratedTypeRegistrar(() => [typeInfo]);
        registrar.RegisterTypesFromAssemblies(services, new AllowAllTypeFilterer(), []);

        var provider = services.BuildServiceProvider();
        var instance1 = provider.GetRequiredService<TestService>();
        var instance2 = provider.GetRequiredService<ITestService>();

        Assert.Same(instance1, instance2);
    }

    private interface ITestService { }

    private sealed class TestService : ITestService { }

    private sealed class AllowAllTypeFilterer : ITypeFilterer
    {
        // These methods return false to indicate no explicit lifetime preference
        // The registrar should use the pre-computed lifetime from InjectableTypeInfo
        public bool IsInjectableScopedType(Type type) => false;
        public bool IsInjectableTransientType(Type type) => false;
        public bool IsInjectableSingletonType(Type type) => false;
    }
}
