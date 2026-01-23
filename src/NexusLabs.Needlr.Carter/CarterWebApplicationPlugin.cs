using Carter;

using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.Carter;

/// <summary>
/// Plugin that configures Carter middleware on the web application.
/// Maps Carter routes after the application has been built.
/// </summary>
public sealed class CarterWebApplicationPlugin : IWebApplicationPlugin
{
    /// <inheritdoc />
    public void Configure(WebApplicationPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.WebApplication.Logger.LogInformation("Configuring Carter middleware...");
        options.WebApplication.MapCarter();
        options.WebApplication.Logger.LogInformation("Carter middleware configured successfully.");
    }
}
