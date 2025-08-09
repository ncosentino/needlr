using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Provides extension methods for configuring <see cref="CreateWebApplicationOptions"/>.
/// </summary>
public static class CreateWebApplicationOptionsExtensions
{
    /// <summary>
    /// Configures the options to use a console logger for startup logging.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="name">The name of the logger. Defaults to "Startup".</param>
    /// <param name="level">The minimum log level. Defaults to <see cref="LogLevel.Debug"/>.</param>
    /// <returns>A new instance of <see cref="CreateWebApplicationOptions"/> with the console logger configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static CreateWebApplicationOptions UsingStartupConsoleLogger(
        this CreateWebApplicationOptions options,
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
    /// <returns>A new instance of <see cref="CreateWebApplicationOptions"/> with the command line arguments configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="args"/> is null.</exception>
    public static CreateWebApplicationOptions UsingCliArgs(
        this CreateWebApplicationOptions options,
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(args);
        
        var newOptions = options with
        {
            Options = new WebApplicationOptions()
            {
                Args = args,
                ApplicationName = options.Options.ApplicationName,
                ContentRootPath = options.Options.ContentRootPath,
                EnvironmentName = options.Options.EnvironmentName,
                WebRootPath = options.Options.WebRootPath
            }
        };

        return newOptions;
    }

    /// <summary>
    /// Configures the options to use the specified application name.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="applicationName">The application name to use.</param>
    /// <returns>A new instance of <see cref="CreateWebApplicationOptions"/> with the application name configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="applicationName"/> is null.</exception>
    public static CreateWebApplicationOptions UsingApplicationName(
        this CreateWebApplicationOptions options,
        string applicationName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(applicationName);

        var newOptions = options with
        {
            Options = new WebApplicationOptions()
            {
                Args = options.Options.Args,
                ApplicationName = applicationName,
                ContentRootPath = options.Options.ContentRootPath,
                EnvironmentName = options.Options.EnvironmentName,
                WebRootPath = options.Options.WebRootPath
            }
        };

        return newOptions;
    }
}