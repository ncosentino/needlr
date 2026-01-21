using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Hosting;

namespace HostBuilderIntegrationExample;

/// <summary>
/// Example plugin that runs during UseNeedlrDiscovery().
/// This demonstrates that plugins still work in the integration pattern.
/// </summary>
public sealed class IntegrationExampleBuilderPlugin : IHostApplicationBuilderPlugin
{
    public void Configure(HostApplicationBuilderPluginOptions options)
    {
        options.Logger.LogInformation(
            "IntegrationExampleBuilderPlugin: Running inside UseNeedlrDiscovery()...");
        
        // You can still add services, configuration, etc. from plugins
        options.Logger.LogInformation(
            "IntegrationExampleBuilderPlugin: Configured.");
    }
}

/// <summary>
/// Example plugin that runs when RunHostPlugins() is called.
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
            "IntegrationExampleHostPlugin: Running from RunHostPlugins()...");
        log?.LogInformation(
            "IntegrationExampleHostPlugin: Post-build configuration complete.");
    }
}
