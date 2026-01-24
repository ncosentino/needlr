using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests for [Intercept] attribute using source-generated interceptor proxies.
/// These tests verify that interceptors are correctly discovered, proxy classes generated,
/// and interceptor chains executed at runtime.
/// </summary>
public sealed class InterceptorSourceGenTests
{
    [Fact]
    public void Intercept_ClassLevel_ProxyIsResolved()
    {
        // Arrange & Act
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Assert - The intercepted service should be resolvable
        var service = serviceProvider.GetService<ILoggingInterceptedService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Intercept_LoggingInterceptor_MethodCallsAreIntercepted()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var service = serviceProvider.GetRequiredService<ILoggingInterceptedService>();
        var result = service.GetValue();

        // Assert - Logging interceptor doesn't modify the result
        Assert.Equal("Original", result);
    }

    [Fact]
    public void Intercept_ModifyingInterceptor_ModifiesReturnValue()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var service = serviceProvider.GetRequiredService<IModifyingInterceptedService>();
        var result = service.GetValue();

        // Assert - Modifying interceptor wraps the result
        Assert.Equal("[Modified:Original]", result);
    }

    [Fact]
    public void Intercept_MultipleInterceptors_ChainIsBuiltCorrectly()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var service = serviceProvider.GetRequiredService<IMultiInterceptedService>();
        var result = service.GetValue();

        // Assert - Order1 (outer) wraps Order2 (inner) wraps Original
        Assert.Equal("Order1(Order2(Original))", result);
    }

    [Fact]
    public async Task Intercept_AsyncMethod_ReturnsCorrectResult()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var service = serviceProvider.GetRequiredService<ILoggingInterceptedService>();
        var result = await service.GetValueAsync();

        // Assert - Logging interceptor doesn't modify async result
        Assert.Equal("AsyncOriginal", result);
    }

    [Fact]
    public void Intercept_MethodWithParameters_ParametersArePassedThrough()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var service = serviceProvider.GetRequiredService<ILoggingInterceptedService>();
        var result = service.Process("TestInput");

        // Assert - Parameters passed through correctly
        Assert.Equal("Processed:TestInput", result);
    }

    [Fact]
    public void Intercept_VoidMethod_ExecutesWithoutError()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var service = serviceProvider.GetRequiredService<ILoggingInterceptedService>();
        
        // Should not throw
        var exception = Record.Exception(() => service.DoWork());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Intercept_ModifyingAsync_ModifiesAsyncResult()
    {
        // Arrange
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act
        var service = serviceProvider.GetRequiredService<IModifyingInterceptedService>();
        var result = await service.GetValueAsync();

        // Assert - Modifying interceptor wraps async result
        Assert.Equal("[Modified:AsyncOriginal]", result);
    }
}
