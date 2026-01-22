using Carter;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.Carter;

/// <summary>
/// Plugin that registers Carter services with the web application builder.
/// Automatically adds Carter module support to the ASP.NET Core application.
/// </summary>
public sealed class CarterWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    /// <inheritdoc />
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Check if Carter has already been configured to prevent duplicate registration
        if (options.Builder.Services.Any(s => s.ServiceType == typeof(CarterConfigurator)))
        {
            options.Logger.LogDebug("Carter services already configured, skipping duplicate registration.");
            return;
        }

        options.Logger.LogInformation("Configuring Carter services...");
        options.Builder.Services.AddCarter();
        options.Logger.LogInformation("Carter services configured successfully.");
    }
}
