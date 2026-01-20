using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.Reflection.TypeRegistrars;
using NexusLabs.Needlr.Injection.SourceGen.TypeRegistrars;

using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify decorator pattern handling is consistent between
/// reflection and source-generation paths. These tests compare both paths
/// to ensure they produce equivalent results.
/// </summary>
public sealed class DecoratorPatternParityTests
{
    private readonly IReadOnlyList<Assembly> _assemblies;
    private readonly ReflectionTypeRegistrar _reflectionRegistrar;
    private readonly GeneratedTypeRegistrar _generatedRegistrar;
    private readonly ReflectionTypeFilterer _typeFilterer;

    public DecoratorPatternParityTests()
    {
        _assemblies = [Assembly.GetExecutingAssembly()];
        _reflectionRegistrar = new ReflectionTypeRegistrar();
        _generatedRegistrar = new GeneratedTypeRegistrar(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes);
        _typeFilterer = new ReflectionTypeFilterer();
    }

    [Fact]
    public void Parity_DecoratorIsRegisteredAsItself_InBothPaths()
    {
        // Arrange
        var reflectionServices = new ServiceCollection();
        var generatedServices = new ServiceCollection();
        
        // Act
        _reflectionRegistrar.RegisterTypesFromAssemblies(reflectionServices, _typeFilterer, _assemblies);
        _generatedRegistrar.RegisterTypesFromAssemblies(generatedServices, _typeFilterer, _assemblies);
        
        // Assert - Both paths should register the decorator as itself
        var reflectionReg = reflectionServices.FirstOrDefault(
            sd => sd.ServiceType == typeof(DecoratorTestServiceDecorator));
        var generatedReg = generatedServices.FirstOrDefault(
            sd => sd.ServiceType == typeof(DecoratorTestServiceDecorator));

        Assert.NotNull(reflectionReg);
        Assert.NotNull(generatedReg);
    }

    [Fact]
    public void Parity_DecoratorIsNotRegisteredAsDecoratedInterface_InBothPaths()
    {
        // Arrange
        var reflectionServices = new ServiceCollection();
        var generatedServices = new ServiceCollection();
        
        // Act
        _reflectionRegistrar.RegisterTypesFromAssemblies(reflectionServices, _typeFilterer, _assemblies);
        _generatedRegistrar.RegisterTypesFromAssemblies(generatedServices, _typeFilterer, _assemblies);
        
        // Assert - Neither path should register the decorator as IDecoratorTestService
        var reflectionDecorator = reflectionServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .Any(sd => sd.ImplementationType == typeof(DecoratorTestServiceDecorator));
        
        var generatedDecorator = generatedServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .Any(sd => sd.ImplementationType == typeof(DecoratorTestServiceDecorator));

        Assert.False(reflectionDecorator, "Reflection path should not register decorator as interface");
        Assert.False(generatedDecorator, "SourceGen path should not register decorator as interface");
    }

    [Fact]
    public void Parity_InnerImplementationIsRegisteredAsInterface_InBothPaths()
    {
        // Arrange
        var reflectionServices = new ServiceCollection();
        var generatedServices = new ServiceCollection();
        
        // Act
        _reflectionRegistrar.RegisterTypesFromAssemblies(reflectionServices, _typeFilterer, _assemblies);
        _generatedRegistrar.RegisterTypesFromAssemblies(generatedServices, _typeFilterer, _assemblies);
        
        // Assert - Both paths should register the inner implementation as IDecoratorTestService
        var reflectionRegs = reflectionServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .ToList();
        
        var generatedRegs = generatedServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .ToList();

        Assert.NotEmpty(reflectionRegs);
        Assert.NotEmpty(generatedRegs);
    }

    [Fact]
    public void Parity_BothPathsRegisterSameInterfaceImplementations()
    {
        // Arrange
        var reflectionServices = new ServiceCollection();
        var generatedServices = new ServiceCollection();
        
        // Act
        _reflectionRegistrar.RegisterTypesFromAssemblies(reflectionServices, _typeFilterer, _assemblies);
        _generatedRegistrar.RegisterTypesFromAssemblies(generatedServices, _typeFilterer, _assemblies);
        
        // Get interface registrations for the decorator test interface
        var reflectionInterfaceRegs = reflectionServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .Select(sd => sd.ImplementationType?.Name ?? "factory")
            .OrderBy(n => n)
            .ToList();
        
        var generatedInterfaceRegs = generatedServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .Select(sd => sd.ImplementationType?.Name ?? "factory")
            .OrderBy(n => n)
            .ToList();
        
        // Assert - Both paths should have same registrations
        Assert.Equal(reflectionInterfaceRegs, generatedInterfaceRegs);
    }

    [Fact]
    public void Parity_MultipleDecoratorsAreAllExcludedFromInterfaceRegistration()
    {
        // Arrange
        var reflectionServices = new ServiceCollection();
        var generatedServices = new ServiceCollection();
        
        // Act
        _reflectionRegistrar.RegisterTypesFromAssemblies(reflectionServices, _typeFilterer, _assemblies);
        _generatedRegistrar.RegisterTypesFromAssemblies(generatedServices, _typeFilterer, _assemblies);
        
        // Assert - Neither decorator should be registered as IDecoratorTestService in either path
        var reflectionDecorators = reflectionServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .Where(sd => sd.ImplementationType == typeof(DecoratorTestServiceDecorator) ||
                        sd.ImplementationType == typeof(DecoratorTestServiceSecondDecorator))
            .ToList();
        
        var generatedDecorators = generatedServices
            .Where(sd => sd.ServiceType == typeof(IDecoratorTestService))
            .Where(sd => sd.ImplementationType == typeof(DecoratorTestServiceDecorator) ||
                        sd.ImplementationType == typeof(DecoratorTestServiceSecondDecorator))
            .ToList();
        
        Assert.Empty(reflectionDecorators);
        Assert.Empty(generatedDecorators);
    }

    [Fact]
    public void Parity_NonDecoratorWithDependencyIsRegisteredAsInterface_InBothPaths()
    {
        // Arrange
        var reflectionServices = new ServiceCollection();
        var generatedServices = new ServiceCollection();
        
        // Act
        _reflectionRegistrar.RegisterTypesFromAssemblies(reflectionServices, _typeFilterer, _assemblies);
        _generatedRegistrar.RegisterTypesFromAssemblies(generatedServices, _typeFilterer, _assemblies);
        
        // Assert - NonDecoratorTestService takes INonDecoratorDependency but implements
        // INonDecoratorTestService - should be registered as INonDecoratorTestService in both paths
        var reflectionReg = reflectionServices.FirstOrDefault(
            sd => sd.ServiceType == typeof(INonDecoratorTestService));
        var generatedReg = generatedServices.FirstOrDefault(
            sd => sd.ServiceType == typeof(INonDecoratorTestService));

        Assert.NotNull(reflectionReg);
        Assert.NotNull(generatedReg);
    }

    [Fact]
    public void Parity_ResolvingInterfaceDoesNotCauseInfiniteLoop_InBothPaths()
    {
        // Arrange
        var reflectionServices = new ServiceCollection();
        var generatedServices = new ServiceCollection();
        
        _reflectionRegistrar.RegisterTypesFromAssemblies(reflectionServices, _typeFilterer, _assemblies);
        _generatedRegistrar.RegisterTypesFromAssemblies(generatedServices, _typeFilterer, _assemblies);
        
        var reflectionProvider = reflectionServices.BuildServiceProvider();
        var generatedProvider = generatedServices.BuildServiceProvider();
        
        // Act & Assert - Neither path should throw StackOverflowException
        var reflectionService = reflectionProvider.GetRequiredService<IDecoratorTestService>();
        var generatedService = generatedProvider.GetRequiredService<IDecoratorTestService>();

        Assert.NotNull(reflectionService);
        Assert.NotNull(generatedService);
        Assert.Equal("Original", reflectionService.GetValue());
        Assert.Equal("Original", generatedService.GetValue());
    }
}
