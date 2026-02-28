using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AspNet;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.SignalR;

/// <summary>
/// Extension methods for configuring SignalR hub registration via Needlr.
/// </summary>
/// <remarks>
/// <para>
/// Two approaches are supported:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Reflection-based</b> (<see cref="UseSignalRHubsWithReflection"/>): discovers and maps hubs at
/// runtime. Convenient but incompatible with AOT/trimmed builds.
/// </description></item>
/// <item><description>
/// <b>Source-generated</b> (<c>app.MapGeneratedHubs()</c>): emits hub mappings at compile time.
/// Required for AOT/trimmed applications.
/// </description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Reflection-based (not AOT-safe)
/// app.UseSignalRHubsWithReflection();
///
/// // Source-generated (AOT-safe) — requires NexusLabs.Needlr.SignalR.Generators
/// app.MapGeneratedHubs();
/// </code>
/// </example>
public static class SignalRExtensions
{
    /// <summary>
    /// Registers SignalR hubs using reflection-based discovery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses reflection to discover <see cref="IHubRegistrationPlugin"/> implementations
    /// and invoke <c>MapHub&lt;T&gt;()</c> at runtime.
    /// </para>
    /// <para>
    /// <strong>For AOT/trimmed applications</strong>, use the source-generated approach instead:
    /// <code>
    /// app.MapGeneratedHubs(); // Generated at compile-time, no reflection
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="app">The web application to configure.</param>
    /// <param name="pluginFactory">The plugin factory to use for discovering hub registration plugins.</param>
    /// <param name="assemblies">The assemblies to scan for hub registration plugins.</param>
    /// <returns>The web application for chaining.</returns>
    [RequiresUnreferencedCode("SignalR hub registration uses reflection to invoke MapHub<T>(). For AOT scenarios, use app.MapGeneratedHubs() instead.")]
    [RequiresDynamicCode("SignalR hub registration uses MakeGenericMethod() which requires dynamic code generation.")]
    public static WebApplication UseSignalRHubsWithReflection(
        this WebApplication app,
        IPluginFactory? pluginFactory = null,
        IEnumerable<System.Reflection.Assembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        pluginFactory ??= app.Services.GetRequiredService<IPluginFactory>();
        assemblies ??= app.Services.GetRequiredService<IReadOnlyList<System.Reflection.Assembly>>();

        var plugin = new SignalRHubRegistrationPlugin();
        var assemblyList = assemblies as IReadOnlyList<System.Reflection.Assembly> ?? assemblies.ToList();
        plugin.Configure(new WebApplicationPluginOptions(app, assemblyList, pluginFactory));

        return app;
    }

    /// <summary>
    /// Adds the reflection-based SignalR hub registration plugin to the service collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This registers the <see cref="SignalRHubRegistrationPlugin"/> which uses reflection
    /// to discover and map SignalR hubs at runtime.
    /// </para>
    /// <para>
    /// <strong>For AOT/trimmed applications</strong>, do not use this method. Instead,
    /// call <c>app.MapGeneratedHubs()</c> directly after building the application.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresUnreferencedCode("SignalR hub registration uses reflection. For AOT scenarios, use app.MapGeneratedHubs() instead.")]
    [RequiresDynamicCode("SignalR hub registration uses MakeGenericMethod() which requires dynamic code generation.")]
    public static IServiceCollection AddSignalRHubRegistrationWithReflection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the plugin so it gets picked up during web application configuration
        services.AddSingleton<IWebApplicationPlugin, SignalRHubRegistrationPlugin>();
        return services;
    }
}
