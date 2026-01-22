using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.Carter;

/// <summary>
/// Plugin that configures Carter middleware on the web application.
/// Maps Carter routes after the application has been built.
/// </summary>
public sealed class CarterWebApplicationPlugin : IWebApplicationPlugin
{
    // Use ConditionalWeakTable to track which WebApplication instances have had MapCarter called
    // This avoids static state issues across different app instances in tests
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WebApplication, object> _mappedApps = new();

    /// <inheritdoc />
    public void Configure(WebApplicationPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Check if MapCarter has already been called for this specific WebApplication instance
        if (_mappedApps.TryGetValue(options.WebApplication, out _))
        {
            options.WebApplication.Logger.LogDebug("Carter routes already mapped for this application, skipping duplicate registration.");
            return;
        }

        options.WebApplication.Logger.LogInformation("Configuring Carter middleware...");
        options.WebApplication.MapCarter();
        _mappedApps.Add(options.WebApplication, new object());
        options.WebApplication.Logger.LogInformation("Carter middleware configured successfully.");
    }
}
