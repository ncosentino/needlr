using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.IntegrationTests.Generated;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests for [GenerateFactory] attribute using source-generated factories.
/// These tests verify that factory interfaces and Func delegates are correctly generated,
/// registered, and can be resolved and used at runtime.
/// </summary>
public sealed class FactorySourceGenTests
{
    [Fact]
    public void Factory_SimpleService_FactoryInterfaceIsResolvable()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetService<ISimpleFactoryServiceFactory>();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Factory_SimpleService_FuncIsResolvable()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetService<Func<string, SimpleFactoryService>>();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Factory_SimpleService_FactoryCreatesInstanceWithRuntimeParam()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<ISimpleFactoryServiceFactory>();

        // Act
        var instance = factory.Create("Server=localhost;Database=test");

        // Assert
        Assert.NotNull(instance);
        Assert.Equal("Server=localhost;Database=test", instance.ConnectionString);
        Assert.NotNull(instance.Dependency);
        Assert.Equal("FactoryDependency", instance.Dependency.Name);
    }

    [Fact]
    public void Factory_SimpleService_FuncCreatesInstanceWithRuntimeParam()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<Func<string, SimpleFactoryService>>();

        // Act
        var instance = factory("Server=localhost;Database=test");

        // Assert
        Assert.NotNull(instance);
        Assert.Equal("Server=localhost;Database=test", instance.ConnectionString);
        Assert.NotNull(instance.Dependency);
    }

    [Fact]
    public void Factory_MultiParam_FactoryCreatesInstanceWithAllRuntimeParams()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<IMultiParamFactoryServiceFactory>();

        // Act
        var instance = factory.Create("localhost", 5432);

        // Assert
        Assert.NotNull(instance);
        Assert.Equal("localhost", instance.Host);
        Assert.Equal(5432, instance.Port);
        Assert.NotNull(instance.Dependency);
    }

    [Fact]
    public void Factory_MultiParam_FuncCreatesInstanceWithAllRuntimeParams()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<Func<string, int, MultiParamFactoryService>>();

        // Act
        var instance = factory("localhost", 5432);

        // Assert
        Assert.NotNull(instance);
        Assert.Equal("localhost", instance.Host);
        Assert.Equal(5432, instance.Port);
    }

    [Fact]
    public void Factory_FuncOnlyMode_FuncIsResolvable()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetService<Func<Guid, FuncOnlyFactoryService>>();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Factory_FuncOnlyMode_InterfaceIsNotGenerated()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - try to get the interface (which shouldn't exist)
        var factoryType = Type.GetType("NexusLabs.Needlr.IntegrationTests.Generated.IFuncOnlyFactoryServiceFactory, NexusLabs.Needlr.IntegrationTests");

        // Assert - type should not exist
        Assert.Null(factoryType);
    }

    [Fact]
    public void Factory_FuncOnlyMode_FuncCreatesInstance()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<Func<Guid, FuncOnlyFactoryService>>();
        var requestId = Guid.NewGuid();

        // Act
        var instance = factory(requestId);

        // Assert
        Assert.NotNull(instance);
        Assert.Equal(requestId, instance.RequestId);
        Assert.NotNull(instance.Dependency);
    }

    [Fact]
    public void Factory_InterfaceOnlyMode_InterfaceIsResolvable()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetService<IInterfaceOnlyFactoryServiceFactory>();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Factory_InterfaceOnlyMode_FuncIsNotRegistered()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var factory = serviceProvider.GetService<Func<DateTime, InterfaceOnlyFactoryService>>();

        // Assert - Func should not be registered
        Assert.Null(factory);
    }

    [Fact]
    public void Factory_InterfaceOnlyMode_InterfaceCreatesInstance()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<IInterfaceOnlyFactoryServiceFactory>();
        var createdAt = new DateTime(2026, 1, 27, 12, 0, 0);

        // Act
        var instance = factory.Create(createdAt);

        // Assert
        Assert.NotNull(instance);
        Assert.Equal(createdAt, instance.CreatedAt);
        Assert.NotNull(instance.Dependency);
    }

    [Fact]
    public void Factory_ServiceTypeNotDirectlyRegistered()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - try to resolve the service directly (should not be registered)
        var service = serviceProvider.GetService<SimpleFactoryService>();

        // Assert - service should NOT be directly resolvable
        Assert.Null(service);
    }

    [Fact]
    public void Factory_FactoryIsSingleton_SameInstanceReturned()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var factory1 = serviceProvider.GetRequiredService<ISimpleFactoryServiceFactory>();
        var factory2 = serviceProvider.GetRequiredService<ISimpleFactoryServiceFactory>();

        // Assert - same factory instance (singleton)
        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void Factory_CreatedInstancesAreNotSame()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<ISimpleFactoryServiceFactory>();

        // Act
        var instance1 = factory.Create("conn1");
        var instance2 = factory.Create("conn2");

        // Assert - different instances
        Assert.NotSame(instance1, instance2);
        Assert.Equal("conn1", instance1.ConnectionString);
        Assert.Equal("conn2", instance2.ConnectionString);
    }

    [Fact]
    public void Factory_MultiConstructor_BothOverloadsAvailable()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<IMultiConstructorFactoryServiceFactory>();

        // Act
        var instance1 = factory.Create("Service1");
        var instance2 = factory.Create("Service2", 30);

        // Assert
        Assert.NotNull(instance1);
        Assert.Equal("Service1", instance1.Name);
        Assert.Null(instance1.Timeout);

        Assert.NotNull(instance2);
        Assert.Equal("Service2", instance2.Name);
        Assert.Equal(30, instance2.Timeout);
    }

    [Fact]
    public void Factory_MultiConstructor_FuncsForBothConstructors()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var func1 = serviceProvider.GetService<Func<string, MultiConstructorFactoryService>>();
        var func2 = serviceProvider.GetService<Func<string, int, MultiConstructorFactoryService>>();

        // Assert
        Assert.NotNull(func1);
        Assert.NotNull(func2);

        var instance1 = func1("Test1");
        var instance2 = func2("Test2", 60);

        Assert.Equal("Test1", instance1.Name);
        Assert.Null(instance1.Timeout);

        Assert.Equal("Test2", instance2.Name);
        Assert.Equal(60, instance2.Timeout);
    }
}
