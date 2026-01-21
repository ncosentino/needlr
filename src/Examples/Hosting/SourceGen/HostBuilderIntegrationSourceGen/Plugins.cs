using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Hosting;

namespace HostBuilderIntegrationSourceGen;

/// <summary>
/// Example plugin that runs during UseNeedlrDiscovery() with source generation.
/// This demonstrates that plugins still work in the integration pattern.
/// </summary>
public sealed class IntegrationExampleBuilderPlugin : IHostApplicationBuilderPlugin
{
    public void Configure(HostApplicationBuilderPluginOptions options)
    {
        options.Logger.LogInformation(
            "IntegrationExampleBuilderPlugin: Running inside UseNeedlrDiscovery() (SourceGen)...");
        
        // You can still add services, configuration, etc. from plugins
        options.Logger.LogInformation(
            "IntegrationExampleBuilderPlugin: Configured.");
    }
}

/// <summary>
/// Example plugin that runs when RunHostPlugins() is called with source generation.
/// This demonstrates the opt-in nature of IHostPlugin in integration mode.
/// </summary>
public sealed class IntegrationExampleHostPlugin : IHostPlugin
{
    public void Configure(HostPluginOptions options)
    {
        var logger = options.Host.Services.GetService(typeof(ILoggerFactory)) 
            as ILoggerFactory;
        var log = logger?.CreateLogger<IntegrationExampleHostPlugin>();
        
        log?.LogInformation(
            "IntegrationExampleHostPlugin: Running from RunHostPlugins() (SourceGen)...");
        log?.LogInformation(
            "IntegrationExampleHostPlugin: Post-build configuration complete.");
    }
}
