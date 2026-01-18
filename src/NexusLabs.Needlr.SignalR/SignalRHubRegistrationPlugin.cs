using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.SignalR;

/// <summary>
/// Plugin that registers SignalR hubs discovered via <see cref="IHubRegistrationPlugin"/> implementations.
/// </summary>
/// <remarks>
/// This plugin uses reflection to invoke MapHub&lt;T&gt;() at runtime because the SignalR API
/// does not provide a non-generic overload. For AOT/trimmed applications, consider using
/// source-generated hub registration instead.
/// </remarks>
[RequiresUnreferencedCode("SignalR hub registration uses reflection to invoke MapHub<T>(). For AOT scenarios, register hubs explicitly.")]
[RequiresDynamicCode("SignalR hub registration uses MakeGenericMethod() which requires dynamic code generation.")]
public sealed class SignalRHubRegistrationPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var pluginFactory = options.WebApplication.Services.GetRequiredService<IPluginFactory>();

        options.WebApplication.Logger.LogInformation("Configuring SignalR hubs...");

        // NOTE: this is gross reflection because there is no overload to use
        // Type objects instead of the generic type parameters
        var mapHubMethod = typeof(HubEndpointRouteBuilderExtensions)
            .GetMethods()
            .First(m => m.Name == "MapHub" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2);

        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(
            options.Assemblies))
        {
            options.WebApplication.Logger.LogInformation("Registering SignalR hub '{HubName}'...", plugin.GetType().Name);
            var genericMapHub = mapHubMethod.MakeGenericMethod(plugin.HubType);
            genericMapHub.Invoke(null, new object[] { options.WebApplication, plugin.HubPath });
        }

        options.WebApplication.Logger.LogInformation("SignalR hubs configured successfully.");
    }
}
