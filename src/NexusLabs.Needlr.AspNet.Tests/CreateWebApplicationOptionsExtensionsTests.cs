using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

public sealed class CreateWebApplicationOptionsExtensionsTests
{
    [Fact]
    public void UsingPostPluginRegistrationCallback_AddsCallback()
    {
        var options = CreateWebApplicationOptions.Default;
        var wasCalled = false;

        var newOptions = options.UsingPostPluginRegistrationCallback(services =>
        {
            wasCalled = true;
        });

        Assert.NotSame(options, newOptions);
        Assert.Single(newOptions.PostPluginRegistrationCallbacks);
        
        var services = new ServiceCollection();
        newOptions.PostPluginRegistrationCallbacks[0](services);
        Assert.True(wasCalled);
    }

    [Fact]
    public void UsingPostPluginRegistrationCallback_PreservesExistingCallbacks()
    {
        var firstCalled = false;
        var secondCalled = false;
        
        var options = CreateWebApplicationOptions.Default
            .UsingPostPluginRegistrationCallback(services => firstCalled = true)
            .UsingPostPluginRegistrationCallback(services => secondCalled = true);

        Assert.Equal(2, options.PostPluginRegistrationCallbacks.Count);
        
        var services = new ServiceCollection();
        foreach (var callback in options.PostPluginRegistrationCallbacks)
        {
            callback(services);
        }
        
        Assert.True(firstCalled);
        Assert.True(secondCalled);
    }

    [Fact]
    public void UsingPostPluginRegistrationCallbacks_ParamsOverload_AddsMultipleCallbacks()
    {
        var callback1Called = false;
        var callback2Called = false;
        var callback3Called = false;

        var options = CreateWebApplicationOptions.Default
            .UsingPostPluginRegistrationCallbacks(
                services => callback1Called = true,
                services => callback2Called = true,
                services => callback3Called = true);

        Assert.Equal(3, options.PostPluginRegistrationCallbacks.Count);
        
        var services = new ServiceCollection();
        foreach (var callback in options.PostPluginRegistrationCallbacks)
        {
            callback(services);
        }
        
        Assert.True(callback1Called);
        Assert.True(callback2Called);
        Assert.True(callback3Called);
    }

    [Fact]
    public void UsingPostPluginRegistrationCallbacks_EnumerableOverload_AddsMultipleCallbacks()
    {
        var callbacks = new List<Action<IServiceCollection>>
        {
            services => services.AddSingleton<ITestService1, TestService1>(),
            services => services.AddSingleton<ITestService2, TestService2>(),
            services => services.AddSingleton<ITestService3, TestService3>()
        };

        var options = CreateWebApplicationOptions.Default
            .UsingPostPluginRegistrationCallbacks(callbacks);

        Assert.Equal(3, options.PostPluginRegistrationCallbacks.Count);
        
        var services = new ServiceCollection();
        foreach (var callback in options.PostPluginRegistrationCallbacks)
        {
            callback(services);
        }
        
        Assert.Equal(3, services.Count);
    }

    [Fact]
    public void UsingPostPluginRegistrationCallback_WithNull_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingPostPluginRegistrationCallback(null!));
    }

    [Fact]
    public void UsingPostPluginRegistrationCallbacks_WithNullArray_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingPostPluginRegistrationCallbacks((Action<IServiceCollection>[])null!));
    }

    [Fact]
    public void UsingPostPluginRegistrationCallbacks_WithNullEnumerable_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingPostPluginRegistrationCallbacks((IEnumerable<Action<IServiceCollection>>)null!));
    }

    [Fact]
    public void FluentChaining_WorksCorrectly()
    {
        var options = CreateWebApplicationOptions.Default
            .UsingStartupConsoleLogger()
            .UsingApplicationName("TestApp")
            .UsingPostPluginRegistrationCallback(services => services.AddLogging())
            .UsingPostPluginRegistrationCallbacks(
                services => services.AddAuthentication(),
                services => services.AddAuthorization())
            .UsingPostPluginRegistrationCallback(services => services.AddAntiforgery());

        Assert.Equal("TestApp", options.Options.ApplicationName);
        Assert.NotNull(options.Logger);
        Assert.Equal(4, options.PostPluginRegistrationCallbacks.Count);
    }

    private interface ITestService1 { }
    private class TestService1 : ITestService1 { }
    
    private interface ITestService2 { }
    private class TestService2 : ITestService2 { }
    
    private interface ITestService3 { }
    private class TestService3 : ITestService3 { }
}