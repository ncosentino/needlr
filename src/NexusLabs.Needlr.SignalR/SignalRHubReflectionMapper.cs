using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;

namespace NexusLabs.Needlr.SignalR;

/// <summary>
/// Maps explicitly requested SignalR hubs through reflection without participating
/// in Needlr plugin discovery.
/// </summary>
[DoNotAutoRegister]
internal sealed partial class SignalRHubReflectionMapper
{
    [RequiresUnreferencedCode("SignalR hub registration uses reflection to invoke MapHub<T>(). For AOT scenarios, use app.MapGeneratedHubs() instead.")]
    [RequiresDynamicCode("SignalR hub registration uses MakeGenericMethod() which requires dynamic code generation.")]
    internal void Map(
        WebApplication app,
        IReadOnlyList<Assembly> assemblies,
        IPluginFactory pluginFactory)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(pluginFactory);

        LogConfiguringHubs(app.Logger);

        var mapHubMethod = typeof(HubEndpointRouteBuilderExtensions)
            .GetMethods()
            .First(method =>
                method.Name == "MapHub" &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 2);

        foreach (var plugin in
            pluginFactory.CreatePluginsFromAssemblies<IHubRegistrationPlugin>(
                assemblies))
        {
            LogRegisteringHub(app.Logger, plugin.GetType().Name);
            var genericMapHub = mapHubMethod.MakeGenericMethod(plugin.HubType);
            genericMapHub.Invoke(
                null,
                [app, plugin.HubPath]);
        }

        LogHubsConfigured(app.Logger);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Configuring SignalR hubs...")]
    private static partial void LogConfiguringHubs(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Registering SignalR hub '{HubName}'...")]
    private static partial void LogRegisteringHub(
        ILogger logger,
        string hubName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SignalR hubs configured successfully.")]
    private static partial void LogHubsConfigured(ILogger logger);
}
