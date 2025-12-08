using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;
using NexusLabs.Needlr.Injection.Scrutor;
using NexusLabs.Needlr.SourceGenerators.Tests.Fixtures;
using System.Reflection;
using Xunit;

namespace NexusLabs.Needlr.SourceGenerators.Tests;

/// <summary>
/// Tests that document the current behavior of the reflection-based registration system.
/// These tests serve as the "golden" reference for what the source generator should produce.
/// </summary>
public class ReflectionBaselineTests
{
    private readonly Assembly _fixtureAssembly;
    private readonly DefaultTypeFilterer _defaultTypeFilterer;

    public ReflectionBaselineTests()
    {
        _fixtureAssembly = typeof(SimpleClass).Assembly;
        _defaultTypeFilterer = new DefaultTypeFilterer();
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersSimpleClass()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(SimpleClass));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(SimpleClass), descriptor.ImplementationType);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassWithPublicParameterlessConstructor()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithPublicParameterlessConstructor));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassWithInjectableClassParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithInjectableClassParameter));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassWithInjectableInterfaceParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithInjectableInterfaceParameter));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersServiceImplementationAsSelfAndInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert - registered as self
        var selfDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ServiceImplementation));
        Assert.NotNull(selfDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, selfDescriptor.Lifetime);

        // Assert - registered as interface
        var interfaceDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ISimpleService));
        Assert.NotNull(interfaceDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, interfaceDescriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersMultiInterfaceImplementation()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert - registered as self
        var selfDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(MultiInterfaceImplementation));
        Assert.NotNull(selfDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, selfDescriptor.Lifetime);

        // Note: Classes with implicit constructors don't get interface registrations
        // because GetConstructors() doesn't return implicit constructors reliably
        // This is the actual behavior we're documenting
        var descriptorsForClass = services.Where(s => s.ImplementationType == typeof(MultiInterfaceImplementation)).ToList();
        Assert.Single(descriptorsForClass); // Only registered as self
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithDoNotAutoRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithDoNotAutoRegister));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterImplementerOfDoNotAutoRegisterInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ImplementsDoNotAutoRegisterInterface));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithDoNotInject()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert - DoNotInject prevents registration even with parameterless constructor
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithDoNotInject));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithDoNotInjectNoParameterlessConstructor()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithDoNotInjectNoParameterlessConstructor));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterAbstractClass()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(AbstractClass));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert - interfaces should not be registered as service types by themselves
        // (only as service types for implementing classes)
        var descriptor = services.FirstOrDefault(s => 
            s.ServiceType == typeof(ITestInterface) && 
            s.ImplementationType == typeof(ITestInterface));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterGenericTypeDefinition()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => 
            s.ServiceType.IsGenericTypeDefinition && 
            s.ServiceType.Name.StartsWith("GenericClassDefinition"));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterNestedClass()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType.IsNested);
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterRecordType()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(TestRecord));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithValueTypeParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithValueTypeParameter));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithStringParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithStringParameter));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithDelegateParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithDelegateParameter));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithMixedParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithMixedParameters));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassWithMultipleConstructors()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithMultipleConstructors));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersInternalClass()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(InternalClass));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassWithPrivateConstructor()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert - DefaultTypeFilterer checks both public and non-public constructors
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithPrivateConstructor));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassWithPrivateParameterlessAndPublicInjectable()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithPrivateParameterlessAndPublicInjectable));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_DoesNotRegisterClassWithCircularConstructorParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithCircularConstructorParameter));
        Assert.Null(descriptor);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassWithAllInjectableParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithAllInjectableParameters));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultTypeRegistrar_RegistersClassImplementingDerivedInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new DefaultTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert - registered as self
        var selfDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassImplementingDerivedInterface));
        Assert.NotNull(selfDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, selfDescriptor.Lifetime);

        // Note: Even with explicit constructor, classes in test fixtures may not get interface registrations
        // if they don't meet all criteria. This documents actual behavior.
        var descriptorsForClass = services.Where(s => s.ImplementationType == typeof(ClassImplementingDerivedInterface)).ToList();
        Assert.Single(descriptorsForClass); // Only registered as self
    }

    [Fact]
    public void ScrutorTypeRegistrar_RegistersSimpleClass()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(SimpleClass));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void ScrutorTypeRegistrar_DoesNotRegisterClassWithDoNotAutoRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ClassWithDoNotAutoRegister));
        Assert.Null(descriptor);
    }

    [Fact]
    public void ScrutorTypeRegistrar_RegistersImplementerOfDoNotAutoRegisterInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        var registrar = new ScrutorTypeRegistrar();

        // Act
        registrar.RegisterTypesFromAssemblies(services, _defaultTypeFilterer, [_fixtureAssembly]);

        // Assert - Scrutor does NOT traverse up inheritance hierarchy for DoNotAutoRegister
        // So this class WILL be registered (documented difference from DefaultTypeRegistrar)
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ImplementsDoNotAutoRegisterInterface));
        Assert.NotNull(descriptor);
    }
}
