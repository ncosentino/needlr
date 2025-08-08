using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.SignalR;

public sealed class SignalRHubRegistrationPlugin : IWebApplicationPlugin
{
    private readonly PluginFactory _pluginFactory = new();

    public void Configure(WebApplicationPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.WebApplication.Logger.LogInformation("Configuring SignalR hubs...");

        // NOTE: this is gross reflection because there is no overload to use
        // Type objects instead of the generic type parameters
        var mapHubMethod = typeof(HubEndpointRouteBuilderExtensions)
            .GetMethods()
            .First(m => m.Name == "MapHub" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2);

        foreach (var plugin in _pluginFactory.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(
            options.Assemblies))
        {
            options.WebApplication.Logger.LogInformation("Registering SignalR hub '{HubName}'...", plugin.GetType().Name);
            var genericMapHub = mapHubMethod.MakeGenericMethod(plugin.HubType);
            genericMapHub.Invoke(null, new object[] { options.WebApplication, plugin.HubPath });
        }

        options.WebApplication.Logger.LogInformation("SignalR hubs configured successfully.");
    }
}
