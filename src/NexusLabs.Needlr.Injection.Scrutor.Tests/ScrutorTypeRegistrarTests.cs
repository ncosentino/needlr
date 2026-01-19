using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Reflection;
using Xunit;

namespace NexusLabs.Needlr.Injection.Scrutor.Tests;

public sealed class ScrutorTypeRegistrarTests
{
    [Fact]
    public void RegisterTypesFromAssemblies_RegistersScopedTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();
        var mockTypeFilterer = new Mock<ITypeFilterer>();

        mockTypeFilterer.Setup(x => x.IsInjectableScopedType(typeof(ScopedService)))
            .Returns(true);
        mockTypeFilterer.Setup(x => x.IsInjectableTransientType(It.IsAny<Type>()))
            .Returns(false);
        mockTypeFilterer.Setup(x => x.IsInjectableSingletonType(It.IsAny<Type>()))
            .Returns(false);

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            mockTypeFilterer.Object,
            [typeof(ScopedService).Assembly]);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ScopedService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_RegistersTransientTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();
        var mockTypeFilterer = new Mock<ITypeFilterer>();

        mockTypeFilterer.Setup(x => x.IsInjectableScopedType(It.IsAny<Type>()))
            .Returns(false);
        mockTypeFilterer.Setup(x => x.IsInjectableTransientType(typeof(TransientService)))
            .Returns(true);
        mockTypeFilterer.Setup(x => x.IsInjectableSingletonType(It.IsAny<Type>()))
            .Returns(false);

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            mockTypeFilterer.Object,
            [typeof(TransientService).Assembly]);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TransientService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_RegistersSingletonTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();
        var mockTypeFilterer = new Mock<ITypeFilterer>();

        mockTypeFilterer.Setup(x => x.IsInjectableScopedType(It.IsAny<Type>()))
            .Returns(false);
        mockTypeFilterer.Setup(x => x.IsInjectableTransientType(It.IsAny<Type>()))
            .Returns(false);
        mockTypeFilterer.Setup(x => x.IsInjectableSingletonType(typeof(SingletonService)))
            .Returns(true);

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            mockTypeFilterer.Object,
            [typeof(SingletonService).Assembly]);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(SingletonService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_RegistersInterfaceTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();
        var mockTypeFilterer = new Mock<ITypeFilterer>();

        mockTypeFilterer.Setup(x => x.IsInjectableScopedType(typeof(ServiceWithInterface)))
            .Returns(true);
        mockTypeFilterer.Setup(x => x.IsInjectableTransientType(It.IsAny<Type>()))
            .Returns(false);
        mockTypeFilterer.Setup(x => x.IsInjectableSingletonType(It.IsAny<Type>()))
            .Returns(false);

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            mockTypeFilterer.Object,
            [typeof(ServiceWithInterface).Assembly]);

        // Assert - Scrutor registers both self and interface
        var concreteDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServiceWithInterface));
        var interfaceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IServiceInterface));
        Assert.NotNull(concreteDescriptor);
        Assert.NotNull(interfaceDescriptor);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_SkipsTypesWithDoNotAutoRegisterAttribute()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();
        var mockTypeFilterer = new Mock<ITypeFilterer>();

        // Note: Scrutor filters by attribute, so even if filterer returns true,
        // the attribute should prevent registration
        mockTypeFilterer.Setup(x => x.IsInjectableScopedType(It.IsAny<Type>()))
            .Returns(true);

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            mockTypeFilterer.Object,
            [typeof(ExcludedService).Assembly]);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ExcludedService));
        Assert.Null(descriptor);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_WithEmptyAssemblyList_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();
        var mockTypeFilterer = new Mock<ITypeFilterer>();

        // Act & Assert
        var exception = Record.Exception(() =>
            registrar.RegisterTypesFromAssemblies(
                services,
                mockTypeFilterer.Object,
                []));
        Assert.Null(exception);
    }

    // Test types
    public class ScopedService { }
    public class TransientService { }
    public class SingletonService { }

    public interface IServiceInterface { }
    public class ServiceWithInterface : IServiceInterface { }

    [DoNotAutoRegister]
    public class ExcludedService { }
}
