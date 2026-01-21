using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Provides extension methods for configuring <see cref="CreateHostOptions"/>.
/// </summary>
public static class CreateHostOptionsExtensions
{
    /// <summary>
    /// Configures the options to use a console logger for startup logging.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="name">The name of the logger. Defaults to "Startup".</param>
    /// <param name="level">The minimum log level. Defaults to <see cref="LogLevel.Debug"/>.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the console logger configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static CreateHostOptions UsingStartupConsoleLogger(
        this CreateHostOptions options,
        string name = "Startup",
        LogLevel level = LogLevel.Debug)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var loggerFactory = LoggerFactory
            .Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(level));
        var logger = loggerFactory.CreateLogger(name);

        var newOptions = options with
        {
            Logger = logger
        };

        return newOptions;
    }

    /// <summary>
    /// Configures the options to use the specified command line arguments.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="args">The command line arguments to use.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the command line arguments configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="args"/> is null.</exception>
    public static CreateHostOptions UsingArgs(
        this CreateHostOptions options,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(args);

        var newOptions = options with
        {
            Settings = new HostApplicationBuilderSettings
            {
                Args = args,
                ApplicationName = options.Settings.ApplicationName,
                ContentRootPath = options.Settings.ContentRootPath,
                EnvironmentName = options.Settings.EnvironmentName,
                Configuration = options.Settings.Configuration,
                DisableDefaults = options.Settings.DisableDefaults
            }
        };

        return newOptions;
    }

    /// <summary>
    /// Configures the options to use the specified application name.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="applicationName">The application name to use.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the application name configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="applicationName"/> is null.</exception>
    public static CreateHostOptions UsingApplicationName(
        this CreateHostOptions options,
        string applicationName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(applicationName);

        var newOptions = options with
        {
            Settings = new HostApplicationBuilderSettings
            {
                Args = options.Settings.Args,
                ApplicationName = applicationName,
                ContentRootPath = options.Settings.ContentRootPath,
                EnvironmentName = options.Settings.EnvironmentName,
                Configuration = options.Settings.Configuration,
                DisableDefaults = options.Settings.DisableDefaults
            }
        };

        return newOptions;
    }

    /// <summary>
    /// Configures the options to use the specified environment name.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="environmentName">The environment name to use.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the environment name configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="environmentName"/> is null.</exception>
    public static CreateHostOptions UsingEnvironmentName(
        this CreateHostOptions options,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environmentName);

        var newOptions = options with
        {
            Settings = new HostApplicationBuilderSettings
            {
                Args = options.Settings.Args,
                ApplicationName = options.Settings.ApplicationName,
                ContentRootPath = options.Settings.ContentRootPath,
                EnvironmentName = environmentName,
                Configuration = options.Settings.Configuration,
                DisableDefaults = options.Settings.DisableDefaults
            }
        };

        return newOptions;
    }

    /// <summary>
    /// Configures the options to use the specified content root path.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="contentRootPath">The content root path to use.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the content root path configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="contentRootPath"/> is null.</exception>
    public static CreateHostOptions UsingContentRootPath(
        this CreateHostOptions options,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contentRootPath);

        var newOptions = options with
        {
            Settings = new HostApplicationBuilderSettings
            {
                Args = options.Settings.Args,
                ApplicationName = options.Settings.ApplicationName,
                ContentRootPath = contentRootPath,
                EnvironmentName = options.Settings.EnvironmentName,
                Configuration = options.Settings.Configuration,
                DisableDefaults = options.Settings.DisableDefaults
            }
        };

        return newOptions;
    }

    /// <summary>
    /// Adds a pre-plugin registration callback to the options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="callback">The callback to add for pre-plugin registration.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the callback added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="callback"/> is null.</exception>
    public static CreateHostOptions UsingPrePluginRegistrationCallback(
        this CreateHostOptions options,
        Action<IServiceCollection> callback)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callback);
        return options.UsingPrePluginRegistrationCallbacks(callback);
    }

    /// <summary>
    /// Adds multiple pre-plugin registration callbacks to the options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="callbacks">The callbacks to add for pre-plugin registration.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the callbacks added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="callbacks"/> is null.</exception>
    public static CreateHostOptions UsingPrePluginRegistrationCallbacks(
        this CreateHostOptions options,
        params Action<IServiceCollection>[] callbacks)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callbacks);
        return options.UsingPrePluginRegistrationCallbacks(callbacks.AsEnumerable());
    }

    /// <summary>
    /// Adds multiple pre-plugin registration callbacks to the options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="callbacks">The callbacks to add for pre-plugin registration.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the callbacks added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="callbacks"/> is null.</exception>
    public static CreateHostOptions UsingPrePluginRegistrationCallbacks(
        this CreateHostOptions options,
        IEnumerable<Action<IServiceCollection>> callbacks)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callbacks);

        var allCallbacks = new List<Action<IServiceCollection>>(options.PrePluginRegistrationCallbacks);
        allCallbacks.AddRange(callbacks);

        var newOptions = options with
        {
            PrePluginRegistrationCallbacks = allCallbacks
        };

        return newOptions;
    }

    /// <summary>
    /// Adds a post-plugin registration callback to the options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="callback">The callback to add for post-plugin registration.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the callback added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="callback"/> is null.</exception>
    public static CreateHostOptions UsingPostPluginRegistrationCallback(
        this CreateHostOptions options,
        Action<IServiceCollection> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return options.UsingPostPluginRegistrationCallbacks(callback);
    }

    /// <summary>
    /// Adds multiple post-plugin registration callbacks to the options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="callbacks">The callbacks to add for post-plugin registration.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the callbacks added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="callbacks"/> is null.</exception>
    public static CreateHostOptions UsingPostPluginRegistrationCallbacks(
        this CreateHostOptions options,
        params Action<IServiceCollection>[] callbacks)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callbacks);
        return options.UsingPostPluginRegistrationCallbacks(callbacks.AsEnumerable());
    }

    /// <summary>
    /// Adds multiple post-plugin registration callbacks to the options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="callbacks">The callbacks to add for post-plugin registration.</param>
    /// <returns>A new instance of <see cref="CreateHostOptions"/> with the callbacks added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="callbacks"/> is null.</exception>
    public static CreateHostOptions UsingPostPluginRegistrationCallbacks(
        this CreateHostOptions options,
        IEnumerable<Action<IServiceCollection>> callbacks)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(callbacks);

        var allCallbacks = new List<Action<IServiceCollection>>(options.PostPluginRegistrationCallbacks);
        allCallbacks.AddRange(callbacks);

        var newOptions = options with
        {
            PostPluginRegistrationCallbacks = allCallbacks
        };

        return newOptions;
    }
}
