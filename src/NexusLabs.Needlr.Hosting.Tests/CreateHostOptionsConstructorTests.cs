using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class CreateHostOptionsConstructorTests
{
    [Fact]
    public void Constructor_Default_UsesNullLogger()
    {
        // Act
        var options = new CreateHostOptions();

        // Assert
        Assert.NotNull(options.Settings);
        Assert.Equal(NullLogger.Instance, options.Logger);
        Assert.Empty(options.PrePluginRegistrationCallbacks);
        Assert.Empty(options.PostPluginRegistrationCallbacks);
    }

    [Fact]
    public void Constructor_WithSettings_UsesProvidedSettings()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings
        {
            ApplicationName = "TestApp",
            EnvironmentName = "Testing"
        };

        // Act
        var options = new CreateHostOptions(settings);

        // Assert
        Assert.Equal("TestApp", options.Settings.ApplicationName);
        Assert.Equal("Testing", options.Settings.EnvironmentName);
    }

    [Fact]
    public void Constructor_WithPrePluginCallbacks_StoresCallbacks()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        var callbacks = new List<Action<IServiceCollection>>
        {
            services => { },
            services => { }
        };

        // Act
        var options = new CreateHostOptions(settings, callbacks);

        // Assert
        Assert.Equal(2, options.PrePluginRegistrationCallbacks.Count);
    }

    [Fact]
    public void Constructor_WithPrePluginCallbacks_NullCallbacks_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        IEnumerable<Action<IServiceCollection>> callbacks = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CreateHostOptions(settings, callbacks));
    }

    [Fact]
    public void Constructor_WithPostPluginCallback_StoresCallback()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        var callbackExecuted = false;
        Action<IServiceCollection> callback = services => callbackExecuted = true;

        // Act
        var options = new CreateHostOptions(settings, callback);

        // Assert
        Assert.Single(options.PostPluginRegistrationCallbacks);
        options.PostPluginRegistrationCallbacks[0].Invoke(new ServiceCollection());
        Assert.True(callbackExecuted);
    }

    [Fact]
    public void Constructor_WithPreAndPostCallbacks_StoresBoth()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        var preCallbacks = new List<Action<IServiceCollection>> { services => { } };
        var postCallbacks = new List<Action<IServiceCollection>> { services => { }, services => { } };

        // Act
        var options = new CreateHostOptions(settings, preCallbacks, postCallbacks);

        // Assert
        Assert.Single(options.PrePluginRegistrationCallbacks);
        Assert.Equal(2, options.PostPluginRegistrationCallbacks.Count);
    }

    [Fact]
    public void Constructor_WithPreAndPostCallbacks_NullPreCallbacks_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        IEnumerable<Action<IServiceCollection>> preCallbacks = null!;
        var postCallbacks = new List<Action<IServiceCollection>> { services => { } };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CreateHostOptions(settings, preCallbacks, postCallbacks));
    }

    [Fact]
    public void Constructor_WithPreAndPostCallbacks_NullPostCallbacks_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        var preCallbacks = new List<Action<IServiceCollection>> { services => { } };
        IEnumerable<Action<IServiceCollection>> postCallbacks = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CreateHostOptions(settings, preCallbacks, postCallbacks));
    }

    [Fact]
    public void Constructor_WithLogger_StoresLogger()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        var logger = NullLogger.Instance;

        // Act
        var options = new CreateHostOptions(settings, logger);

        // Assert
        Assert.Equal(logger, options.Logger);
    }

    [Fact]
    public void Constructor_WithLogger_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        ILogger logger = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CreateHostOptions(settings, logger));
    }

    [Fact]
    public void Constructor_WithPreCallbacksAndLogger_StoresLogger()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        var preCallbacks = new List<Action<IServiceCollection>> { services => { } };
        var logger = NullLogger.Instance;

        // Act
        var options = new CreateHostOptions(settings, preCallbacks, logger);

        // Assert
        // Note: PrePluginRegistrationCallbacks is set via init property, 
        // verifying logger is stored correctly
        Assert.Equal(logger, options.Logger);
    }

    [Fact]
    public void Constructor_WithPostCallbackAndLogger_StoresBoth()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        Action<IServiceCollection> postCallback = services => { };
        var logger = NullLogger.Instance;

        // Act
        var options = new CreateHostOptions(settings, postCallback, logger);

        // Assert
        Assert.Single(options.PostPluginRegistrationCallbacks);
        Assert.Equal(logger, options.Logger);
    }

    [Fact]
    public void Constructor_WithPrePostAndLogger_StoresAll()
    {
        // Arrange
        var settings = new HostApplicationBuilderSettings();
        var preCallbacks = new List<Action<IServiceCollection>> { services => { } };
        var postCallbacks = new List<Action<IServiceCollection>> { services => { } };
        var logger = NullLogger.Instance;

        // Act
        var options = new CreateHostOptions(settings, preCallbacks, postCallbacks, logger);

        // Assert
        Assert.Single(options.PrePluginRegistrationCallbacks);
        Assert.Single(options.PostPluginRegistrationCallbacks);
        Assert.Equal(logger, options.Logger);
    }

    [Fact]
    public void Default_ReturnsStaticInstance()
    {
        // Act
        var options1 = CreateHostOptions.Default;
        var options2 = CreateHostOptions.Default;

        // Assert
        Assert.Same(options1, options2);
    }
}
