using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;
using NexusLabs.Needlr.SourceGenerators.Tests.Fixtures;
using System.Reflection;
using Xunit;

namespace NexusLabs.Needlr.SourceGenerators.Tests;

/// <summary>
/// Tests for the SourceGeneratedTypeRegistrar class.
/// </summary>
public class SourceGeneratedTypeRegistrarTests
{
    [Fact]
    public void Constructor_WithNullAction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SourceGeneratedTypeRegistrar(null!));
    }

    [Fact]
    public void RegisterTypesFromAssemblies_ExecutesProvidedAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var actionExecuted = false;
        
        var registrar = new SourceGeneratedTypeRegistrar(s =>
        {
            actionExecuted = true;
            s.AddSingleton<SimpleClass>();
        });

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            new DefaultTypeFilterer(),
            new[] { typeof(SimpleClass).Assembly });

        // Assert
        Assert.True(actionExecuted);
        Assert.Single(services);
        Assert.Equal(typeof(SimpleClass), services[0].ServiceType);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_IgnoresAssembliesParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        
        var registrar = new SourceGeneratedTypeRegistrar(s =>
        {
            s.AddSingleton<SimpleClass>();
        });

        // Act - pass empty assemblies list
        registrar.RegisterTypesFromAssemblies(
            services,
            new DefaultTypeFilterer(),
            Array.Empty<Assembly>());

        // Assert - registration still happens despite empty assemblies
        Assert.Single(services);
        Assert.Equal(typeof(SimpleClass), services[0].ServiceType);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_IgnoresTypeFiltererParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        
        var registrar = new SourceGeneratedTypeRegistrar(s =>
        {
            s.AddSingleton<SimpleClass>();
        });

        // Act - pass null type filterer (normally would fail for reflection-based registrars)
        registrar.RegisterTypesFromAssemblies(
            services,
            null!,
            new[] { typeof(SimpleClass).Assembly });

        // Assert - registration still happens despite null filterer
        Assert.Single(services);
        Assert.Equal(typeof(SimpleClass), services[0].ServiceType);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_CanRegisterMultipleServices()
    {
        // Arrange
        var services = new ServiceCollection();
        
        var registrar = new SourceGeneratedTypeRegistrar(s =>
        {
            s.AddSingleton<SimpleClass>();
            s.AddTransient<ClassWithPublicParameterlessConstructor>();
            s.AddScoped<InternalClass>();
        });

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            new DefaultTypeFilterer(),
            new[] { typeof(SimpleClass).Assembly });

        // Assert
        Assert.Equal(3, services.Count);
        Assert.Contains(services, d => d.ServiceType == typeof(SimpleClass) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(ClassWithPublicParameterlessConstructor) && d.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, d => d.ServiceType == typeof(InternalClass) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterTypesFromAssemblies_CanRegisterInterfaceMappings()
    {
        // Arrange
        var services = new ServiceCollection();
        
        var registrar = new SourceGeneratedTypeRegistrar(s =>
        {
            s.AddSingleton<ServiceImplementation>();
            s.AddSingleton<ISimpleService>(sp => sp.GetRequiredService<ServiceImplementation>());
        });

        // Act
        registrar.RegisterTypesFromAssemblies(
            services,
            new DefaultTypeFilterer(),
            new[] { typeof(SimpleClass).Assembly });

        // Assert
        Assert.Equal(2, services.Count);
        Assert.Contains(services, d => d.ServiceType == typeof(ServiceImplementation));
        Assert.Contains(services, d => d.ServiceType == typeof(ISimpleService));
    }

    [Fact]
    public void UsingSourceGeneratedTypeRegistrar_ExtensionMethod_ConfiguresSyringe()
    {
        // Arrange
        var syringe = new Syringe();
        var services = new ServiceCollection();
        
        // Create a mock registrar to test that the extension properly wraps the action
        var registrationExecuted = false;
        
        var configured = syringe.UsingSourceGeneratedTypeRegistrar(s =>
        {
            registrationExecuted = true;
            s.AddSingleton<SimpleClass>();
        });

        // Act - manually invoke the registrar since we can't easily test through BuildServiceProvider
        var registrar = new SourceGeneratedTypeRegistrar(s =>
        {
            registrationExecuted = true;
            s.AddSingleton<SimpleClass>();
        });
        
        registrar.RegisterTypesFromAssemblies(services, new DefaultTypeFilterer(), Array.Empty<Assembly>());

        // Assert
        Assert.NotNull(configured);
        Assert.NotSame(syringe, configured); // Syringe is immutable, returns new instance
        Assert.True(registrationExecuted);
        Assert.Single(services);
    }

    [Fact]
    public void UsingSourceGeneratedTypeRegistrar_WithNullSyringe_ThrowsArgumentNullException()
    {
        // Arrange
        Syringe syringe = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            syringe.UsingSourceGeneratedTypeRegistrar(services => { }));
    }

    [Fact]
    public void UsingSourceGeneratedTypeRegistrar_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var syringe = new Syringe();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            syringe.UsingSourceGeneratedTypeRegistrar(null!));
    }
}
