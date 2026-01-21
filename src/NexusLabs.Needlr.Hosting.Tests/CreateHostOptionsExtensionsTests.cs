using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class CreateHostOptionsExtensionsTests
{
    [Fact]
    public void UsingPostPluginRegistrationCallback_AddsCallback()
    {
        var options = CreateHostOptions.Default;
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

        var options = CreateHostOptions.Default
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

        var options = CreateHostOptions.Default
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

        var options = CreateHostOptions.Default
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
        var options = CreateHostOptions.Default;

        Assert.Throws<ArgumentNullException>(() =>
            options.UsingPostPluginRegistrationCallback(null!));
    }

    [Fact]
    public void UsingPostPluginRegistrationCallbacks_WithNullArray_ThrowsArgumentNullException()
    {
        var options = CreateHostOptions.Default;

        Assert.Throws<ArgumentNullException>(() =>
            options.UsingPostPluginRegistrationCallbacks((Action<IServiceCollection>[])null!));
    }

    [Fact]
    public void UsingPostPluginRegistrationCallbacks_WithNullEnumerable_ThrowsArgumentNullException()
    {
        var options = CreateHostOptions.Default;

        Assert.Throws<ArgumentNullException>(() =>
            options.UsingPostPluginRegistrationCallbacks((IEnumerable<Action<IServiceCollection>>)null!));
    }

    [Fact]
    public void UsingArgs_SetsArgs()
    {
        var args = new[] { "--test", "value" };
        var options = CreateHostOptions.Default
            .UsingArgs(args);

        Assert.Equal(args, options.Settings.Args);
    }

    [Fact]
    public void UsingApplicationName_SetsApplicationName()
    {
        var options = CreateHostOptions.Default
            .UsingApplicationName("TestApp");

        Assert.Equal("TestApp", options.Settings.ApplicationName);
    }

    [Fact]
    public void UsingEnvironmentName_SetsEnvironmentName()
    {
        var options = CreateHostOptions.Default
            .UsingEnvironmentName("Development");

        Assert.Equal("Development", options.Settings.EnvironmentName);
    }

    [Fact]
    public void UsingContentRootPath_SetsContentRootPath()
    {
        var options = CreateHostOptions.Default
            .UsingContentRootPath("/app");

        Assert.Equal("/app", options.Settings.ContentRootPath);
    }

    [Fact]
    public void FluentChaining_WorksCorrectly()
    {
        var options = CreateHostOptions.Default
            .UsingStartupConsoleLogger()
            .UsingApplicationName("TestApp")
            .UsingEnvironmentName("Development")
            .UsingPostPluginRegistrationCallback(services => services.AddLogging())
            .UsingPostPluginRegistrationCallbacks(
                services => { },
                services => { })
            .UsingPostPluginRegistrationCallback(services => { });

        Assert.Equal("TestApp", options.Settings.ApplicationName);
        Assert.Equal("Development", options.Settings.EnvironmentName);
        Assert.NotNull(options.Logger);
        Assert.Equal(4, options.PostPluginRegistrationCallbacks.Count);
    }

    [Fact]
    public void UsingPrePluginRegistrationCallback_AddsCallback()
    {
        var options = CreateHostOptions.Default;
        var wasCalled = false;

        var newOptions = options.UsingPrePluginRegistrationCallback(services =>
        {
            wasCalled = true;
        });

        Assert.NotSame(options, newOptions);
        Assert.Single(newOptions.PrePluginRegistrationCallbacks);

        var services = new ServiceCollection();
        newOptions.PrePluginRegistrationCallbacks[0](services);
        Assert.True(wasCalled);
    }

    [Fact]
    public void UsingPrePluginRegistrationCallbacks_ParamsOverload_AddsMultipleCallbacks()
    {
        var callback1Called = false;
        var callback2Called = false;

        var options = CreateHostOptions.Default
            .UsingPrePluginRegistrationCallbacks(
                services => callback1Called = true,
                services => callback2Called = true);

        Assert.Equal(2, options.PrePluginRegistrationCallbacks.Count);

        var services = new ServiceCollection();
        foreach (var callback in options.PrePluginRegistrationCallbacks)
        {
            callback(services);
        }

        Assert.True(callback1Called);
        Assert.True(callback2Called);
    }

    private interface ITestService1 { }
    private class TestService1 : ITestService1 { }

    private interface ITestService2 { }
    private class TestService2 : ITestService2 { }

    private interface ITestService3 { }
    private class TestService3 : ITestService3 { }
}
