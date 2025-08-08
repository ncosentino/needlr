using Carter;

using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.Carter;

public sealed class CarterWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Logger.LogInformation("Configuring Carter services...");
        options.Builder.Services.AddCarter();
        options.Logger.LogInformation("Carter services configured successfully.");
    }
}
