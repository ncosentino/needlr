using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.AspNet;

public static class CreateWebApplicationOptionsExtensions
{
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