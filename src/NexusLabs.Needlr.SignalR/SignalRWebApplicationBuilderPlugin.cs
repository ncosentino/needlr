using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.SignalR;

/// <summary>
/// Plugin that registers SignalR services with the web application builder.
/// Automatically adds SignalR support to the ASP.NET Core application.
/// </summary>
public sealed class SignalRWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    /// <inheritdoc />
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Logger.LogInformation("Configuring SignalR services...");
        options.Builder.Services.AddSignalR();
        options.Logger.LogInformation("SignalR services configured successfully.");
    }
}