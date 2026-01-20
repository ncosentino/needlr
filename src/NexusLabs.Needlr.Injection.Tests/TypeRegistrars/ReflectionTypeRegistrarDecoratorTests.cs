using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.Reflection.TypeRegistrars;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeRegistrars;

/// <summary>
/// Tests that verify the reflection path correctly handles decorator patterns.
/// Decorators (types that implement an interface AND take that interface as a 
/// constructor parameter) should be registered as themselves but NOT as the 
/// interface they decorate.
/// </summary>
public sealed class ReflectionTypeRegistrarDecoratorTests
{
    private readonly IReadOnlyList<Assembly> _assemblies;
    private readonly ReflectionTypeRegistrar _registrar;
    private readonly ReflectionTypeFilterer _typeFilterer;

    public ReflectionTypeRegistrarDecoratorTests()
    {
        _assemblies = [Assembly.GetExecutingAssembly()];
        _registrar = new ReflectionTypeRegistrar();
        _typeFilterer = new ReflectionTypeFilterer();
    }

    [Fact]
    public void DecoratorIsRegisteredAsItself()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        _registrar.RegisterTypesFromAssemblies(services, _typeFilterer, _assemblies);
        
        // Assert - Decorator should be registered as itself
        var decoratorRegistration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(DecoratorServiceDecorator));
        Assert.NotNull(decoratorRegistration);
    }

    [Fact]
    public void DecoratorIsNotRegisteredAsDecoratedInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        _registrar.RegisterTypesFromAssemblies(services, _typeFilterer, _assemblies);
        
        // Assert - Decorator should NOT be registered as IDecoratorService
        var interfaceRegistrations = services
            .Where(sd => sd.ServiceType == typeof(IDecoratorService))
            .ToList();
        
        Assert.DoesNotContain(interfaceRegistrations,
            sd => sd.ImplementationType == typeof(DecoratorServiceDecorator));
    }

    [Fact]
    public void InnerImplementationIsRegisteredAsInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        _registrar.RegisterTypesFromAssemblies(services, _typeFilterer, _assemblies);
        
        // Assert - Inner implementation should be registered as IDecoratorService
        var interfaceRegistrations = services
            .Where(sd => sd.ServiceType == typeof(IDecoratorService))
            .ToList();
        
        Assert.NotEmpty(interfaceRegistrations);
    }

    [Fact]
    public void NonDecoratorWithDependencyIsRegisteredAsInterface()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        _registrar.RegisterTypesFromAssemblies(services, _typeFilterer, _assemblies);
        
        // Assert - NonDecoratorService takes IDependency but implements
        // INonDecoratorService - should be registered as INonDecoratorService
        var interfaceRegistration = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(INonDecoratorService));
        Assert.NotNull(interfaceRegistration);
    }

    [Fact]
    public void ResolvingInterfaceDoesNotCauseInfiniteLoop()
    {
        // Arrange
        var services = new ServiceCollection();
        _registrar.RegisterTypesFromAssemblies(services, _typeFilterer, _assemblies);
        var provider = services.BuildServiceProvider();
        
        // Act & Assert - This should not throw StackOverflowException
        var service = provider.GetRequiredService<IDecoratorService>();
        Assert.NotNull(service);
        Assert.Equal("Original", service.GetValue());
    }

    [Fact]
    public void MultipleDecoratorsAreAllExcludedFromInterfaceRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        _registrar.RegisterTypesFromAssemblies(services, _typeFilterer, _assemblies);
        
        // Assert - Neither decorator should be registered as IDecoratorService
        var decoratorRegs = services
            .Where(sd => sd.ServiceType == typeof(IDecoratorService))
            .Where(sd => sd.ImplementationType == typeof(DecoratorServiceDecorator) ||
                        sd.ImplementationType == typeof(DecoratorServiceSecondDecorator))
            .ToList();
        
        Assert.Empty(decoratorRegs);
    }
}

// ============================================================================
// Test Types for Decorator Pattern Tests
// ============================================================================

public interface IDecoratorService
{
    string GetValue();
}

public sealed class DecoratorServiceImpl : IDecoratorService
{
    public string GetValue() => "Original";
}

public sealed class DecoratorServiceDecorator : IDecoratorService
{
    private readonly IDecoratorService _inner;

    public DecoratorServiceDecorator(IDecoratorService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Decorated({_inner.GetValue()})";
}

public sealed class DecoratorServiceSecondDecorator : IDecoratorService
{
    private readonly IDecoratorService _inner;

    public DecoratorServiceSecondDecorator(IDecoratorService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"SecondDecorated({_inner.GetValue()})";
}

public interface INonDecoratorService
{
    string Process();
}

public interface IDependency
{
    string GetData();
}

public sealed class DependencyImpl : IDependency
{
    public string GetData() => "Data";
}

public sealed class NonDecoratorService : INonDecoratorService
{
    private readonly IDependency _dependency;

    public NonDecoratorService(IDependency dependency)
    {
        _dependency = dependency;
    }

    public string Process() => $"Processed: {_dependency.GetData()}";
}
