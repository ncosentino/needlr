using Carter;

using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.Carter;

public sealed class CarterWebApplicationPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.WebApplication.Logger.LogInformation("Configuring Carter middleware...");
        options.WebApplication.MapCarter();
        options.WebApplication.Logger.LogInformation("Carter middleware configured successfully.");
    }
}
