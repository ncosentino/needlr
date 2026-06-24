using Carter;

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

        options.Logger.LogInformation("Configuring Carter services...");

        // Disable Carter's own assembly scan for ICarterModule implementers: Needlr's type
        // registry is the single source of truth for module registration. Without this, a
        // public module is discovered by BOTH Carter and Needlr and mapped twice, which
        // surfaces as an AmbiguousMatchException at request time. MapCarter() maps whatever
        // ICarterModule services are in the container, so Needlr-registered modules still map.
        options.Builder.Services.AddCarter(configurator: configurator => configurator.WithEmptyModules());

        options.Logger.LogInformation("Carter services configured successfully.");
    }
}
