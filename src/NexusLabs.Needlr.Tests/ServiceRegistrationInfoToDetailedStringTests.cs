using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for ServiceRegistrationInfo.ToDetailedString() debug output.
/// </summary>
public sealed class ServiceRegistrationInfoToDetailedStringTests
{
    [Fact]
    public void ToDetailedString_WithSimpleTransientRegistration_ReturnsFormattedOutput()
    {
        // Arrange
        var descriptor = new ServiceDescriptor(typeof(ITestService), typeof(TestService), ServiceLifetime.Transient);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert
        Assert.Contains("ITestService", result);
        Assert.Contains("TestService", result);
        Assert.Contains("Transient", result);
    }

    [Fact]
    public void ToDetailedString_WithScopedRegistration_ShowsScopedLifetime()
    {
        // Arrange
        var descriptor = new ServiceDescriptor(typeof(ITestService), typeof(TestService), ServiceLifetime.Scoped);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert
        Assert.Contains("Scoped", result);
    }

    [Fact]
    public void ToDetailedString_WithSingletonRegistration_ShowsSingletonLifetime()
    {
        // Arrange
        var descriptor = new ServiceDescriptor(typeof(ITestService), typeof(TestService), ServiceLifetime.Singleton);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert
        Assert.Contains("Singleton", result);
    }

    [Fact]
    public void ToDetailedString_WithFactoryRegistration_IndicatesFactory()
    {
        // Arrange
        var descriptor = new ServiceDescriptor(typeof(ITestService), _ => new TestService(), ServiceLifetime.Transient);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert
        Assert.Contains("ITestService", result);
        Assert.Contains("Factory", result);
    }

    [Fact]
    public void ToDetailedString_WithInstanceRegistration_IndicatesInstance()
    {
        // Arrange
        var instance = new TestService();
        var descriptor = new ServiceDescriptor(typeof(ITestService), instance);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert
        Assert.Contains("ITestService", result);
        Assert.Contains("Instance", result);
    }

    [Fact]
    public void ToDetailedString_WithOpenGenericRegistration_ShowsGenericDefinition()
    {
        // Arrange
        var descriptor = new ServiceDescriptor(typeof(IGenericService<>), typeof(GenericService<>), ServiceLifetime.Transient);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert
        Assert.Contains("IGenericService<>", result);
        Assert.Contains("GenericService<>", result);
    }

    [Fact]
    public void ToDetailedString_WithClosedGenericRegistration_ShowsGenericArguments()
    {
        // Arrange
        var descriptor = new ServiceDescriptor(typeof(IGenericService<string>), typeof(GenericService<string>), ServiceLifetime.Transient);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert
        Assert.Contains("IGenericService<String>", result);
        Assert.Contains("GenericService<String>", result);
    }

    [Fact]
    public void ToDetailedString_OutputIsMultiLine()
    {
        // Arrange
        var descriptor = new ServiceDescriptor(typeof(ITestService), typeof(TestService), ServiceLifetime.Singleton);
        var info = new ServiceRegistrationInfo(descriptor);

        // Act
        var result = info.ToDetailedString();

        // Assert - should have structured multi-line output
        Assert.Contains(Environment.NewLine, result);
    }

    public interface ITestService { }
    public class TestService : ITestService { }
    public interface IGenericService<T> { }
    public class GenericService<T> : IGenericService<T> { }
}
