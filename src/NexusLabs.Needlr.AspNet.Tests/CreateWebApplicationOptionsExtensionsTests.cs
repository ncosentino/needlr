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

    #region PrePluginRegistrationCallback Tests

    [Fact]
    public void UsingPrePluginRegistrationCallback_AddsCallback()
    {
        var options = CreateWebApplicationOptions.Default;
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
    public void UsingPrePluginRegistrationCallback_PreservesExistingCallbacks()
    {
        var firstCalled = false;
        var secondCalled = false;
        
        var options = CreateWebApplicationOptions.Default
            .UsingPrePluginRegistrationCallback(services => firstCalled = true)
            .UsingPrePluginRegistrationCallback(services => secondCalled = true);

        Assert.Equal(2, options.PrePluginRegistrationCallbacks.Count);
        
        var services = new ServiceCollection();
        foreach (var callback in options.PrePluginRegistrationCallbacks)
        {
            callback(services);
        }
        
        Assert.True(firstCalled);
        Assert.True(secondCalled);
    }

    [Fact]
    public void UsingPrePluginRegistrationCallbacks_ParamsOverload_AddsMultipleCallbacks()
    {
        var callback1Called = false;
        var callback2Called = false;
        var callback3Called = false;

        var options = CreateWebApplicationOptions.Default
            .UsingPrePluginRegistrationCallbacks(
                services => callback1Called = true,
                services => callback2Called = true,
                services => callback3Called = true);

        Assert.Equal(3, options.PrePluginRegistrationCallbacks.Count);
        
        var services = new ServiceCollection();
        foreach (var callback in options.PrePluginRegistrationCallbacks)
        {
            callback(services);
        }
        
        Assert.True(callback1Called);
        Assert.True(callback2Called);
        Assert.True(callback3Called);
    }

    [Fact]
    public void UsingPrePluginRegistrationCallbacks_EnumerableOverload_AddsMultipleCallbacks()
    {
        var callbacks = new List<Action<IServiceCollection>>
        {
            services => services.AddSingleton<ITestService1, TestService1>(),
            services => services.AddSingleton<ITestService2, TestService2>(),
            services => services.AddSingleton<ITestService3, TestService3>()
        };

        var options = CreateWebApplicationOptions.Default
            .UsingPrePluginRegistrationCallbacks(callbacks);

        Assert.Equal(3, options.PrePluginRegistrationCallbacks.Count);
        
        var services = new ServiceCollection();
        foreach (var callback in options.PrePluginRegistrationCallbacks)
        {
            callback(services);
        }
        
        Assert.Equal(3, services.Count);
    }

    [Fact]
    public void UsingPrePluginRegistrationCallback_WithNull_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingPrePluginRegistrationCallback(null!));
    }

    [Fact]
    public void UsingPrePluginRegistrationCallbacks_WithNullArray_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingPrePluginRegistrationCallbacks((Action<IServiceCollection>[])null!));
    }

    [Fact]
    public void UsingPrePluginRegistrationCallbacks_WithNullEnumerable_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingPrePluginRegistrationCallbacks((IEnumerable<Action<IServiceCollection>>)null!));
    }

    #endregion

    #region UsingStartupConsoleLogger Tests

    [Fact]
    public void UsingStartupConsoleLogger_WithDefaults_CreatesLogger()
    {
        var options = CreateWebApplicationOptions.Default
            .UsingStartupConsoleLogger();

        Assert.NotNull(options.Logger);
    }

    [Fact]
    public void UsingStartupConsoleLogger_WithCustomName_CreatesLogger()
    {
        var options = CreateWebApplicationOptions.Default
            .UsingStartupConsoleLogger(name: "CustomName");

        Assert.NotNull(options.Logger);
    }

    [Fact]
    public void UsingStartupConsoleLogger_WithCustomLevel_CreatesLogger()
    {
        var options = CreateWebApplicationOptions.Default
            .UsingStartupConsoleLogger(level: Microsoft.Extensions.Logging.LogLevel.Information);

        Assert.NotNull(options.Logger);
    }

    [Fact]
    public void UsingStartupConsoleLogger_WithNullName_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingStartupConsoleLogger(name: null!));
    }

    [Fact]
    public void UsingStartupConsoleLogger_WithWhitespaceName_ThrowsArgumentException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentException>(() => 
            options.UsingStartupConsoleLogger(name: "   "));
    }

    [Fact]
    public void UsingStartupConsoleLogger_WithNullOptions_ThrowsArgumentNullException()
    {
        CreateWebApplicationOptions options = null!;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingStartupConsoleLogger());
    }

    #endregion

    #region UsingCliArgs Tests

    [Fact]
    public void UsingCliArgs_SetsArguments()
    {
        var args = new[] { "--arg1", "value1" };
        var options = CreateWebApplicationOptions.Default
            .UsingCliArgs(args);

        Assert.Equal(args, options.Options.Args);
    }

    [Fact]
    public void UsingCliArgs_PreservesApplicationName()
    {
        var options = CreateWebApplicationOptions.Default
            .UsingApplicationName("MyApp")
            .UsingCliArgs(new[] { "--test" });

        Assert.Equal("MyApp", options.Options.ApplicationName);
        Assert.Equal(new[] { "--test" }, options.Options.Args);
    }

    [Fact]
    public void UsingCliArgs_WithNullOptions_ThrowsArgumentNullException()
    {
        CreateWebApplicationOptions options = null!;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingCliArgs(new[] { "arg" }));
    }

    [Fact]
    public void UsingCliArgs_WithNullArgs_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingCliArgs(null!));
    }

    #endregion

    #region UsingApplicationName Tests

    [Fact]
    public void UsingApplicationName_SetsApplicationName()
    {
        var options = CreateWebApplicationOptions.Default
            .UsingApplicationName("TestApp");

        Assert.Equal("TestApp", options.Options.ApplicationName);
    }

    [Fact]
    public void UsingApplicationName_PreservesArgs()
    {
        var args = new[] { "--test" };
        var options = CreateWebApplicationOptions.Default
            .UsingCliArgs(args)
            .UsingApplicationName("MyApp");

        Assert.Equal("MyApp", options.Options.ApplicationName);
        Assert.Equal(args, options.Options.Args);
    }

    [Fact]
    public void UsingApplicationName_WithNullOptions_ThrowsArgumentNullException()
    {
        CreateWebApplicationOptions options = null!;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingApplicationName("App"));
    }

    [Fact]
    public void UsingApplicationName_WithNullName_ThrowsArgumentNullException()
    {
        var options = CreateWebApplicationOptions.Default;
        
        Assert.Throws<ArgumentNullException>(() => 
            options.UsingApplicationName(null!));
    }

    #endregion
}