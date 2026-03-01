using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr;

namespace MultiProjectApp.Bootstrap;

/// <summary>
/// Aggregates all known feature plugins for this application.
/// Entry points reference only this project to pull in all features at compile time.
/// </summary>
/// <remarks>
/// Note: [DoNotAutoRegister] is intentionally NOT applied here — this plugin participates
/// in plugin discovery normally. Previous versions of Needlr had a bug where applying
/// [DoNotAutoRegister] to a plugin class would silently suppress discovery; this example
/// intentionally omits it to demonstrate the correct pattern.
/// </remarks>
public sealed class BootstrapPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Intentionally empty — feature plugins self-register via Needlr's TypeRegistry.
        // This class exists solely to force the Bootstrap assembly (and its transitive
        // references to both feature projects) into the Needlr plugin scan.
    }
}
