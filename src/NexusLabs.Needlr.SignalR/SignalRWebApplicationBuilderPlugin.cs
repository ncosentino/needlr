using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.SignalR;

public sealed class SignalRWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Logger.LogInformation("Configuring SignalR services...");
        options.Builder.Services.AddSignalR();
        options.Logger.LogInformation("SignalR services configured successfully.");
    }
}