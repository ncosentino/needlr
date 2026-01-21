using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Hosting;

namespace WorkerServiceExample;

/// <summary>
/// Example plugin that demonstrates IHostApplicationBuilderPlugin.
/// This plugin configures the host application builder before the host is built.
/// </summary>
public sealed class ExampleHostApplicationBuilderPlugin : IHostApplicationBuilderPlugin
{
    public void Configure(HostApplicationBuilderPluginOptions options)
    {
        options.Logger.LogInformation("ExampleHostApplicationBuilderPlugin: Configuring...");
        
        // You can configure services, configuration, or logging here
        // For example:
        // options.Builder.Services.AddSingleton<ISomeService, SomeService>();
        // options.Builder.Configuration.AddJsonFile("custom.json", optional: true);
        
        options.Logger.LogInformation("ExampleHostApplicationBuilderPlugin: Configured.");
    }
}

/// <summary>
/// Example plugin that demonstrates IHostPlugin.
/// This plugin runs after the host is built but before it starts running.
/// </summary>
public sealed class ExampleHostPlugin : IHostPlugin
{
    public void Configure(HostPluginOptions options)
    {
        // Access the built host and its services
        var logger = options.Host.Services.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory)) 
            as Microsoft.Extensions.Logging.ILoggerFactory;
        var log = logger?.CreateLogger<ExampleHostPlugin>();
        
        log?.LogInformation("ExampleHostPlugin: Host has been built. Performing post-build configuration...");
        
        // You can access the IHost and its services here
        // This is useful for configuration that requires the service provider
        
        log?.LogInformation("ExampleHostPlugin: Post-build configuration complete.");
    }
}
