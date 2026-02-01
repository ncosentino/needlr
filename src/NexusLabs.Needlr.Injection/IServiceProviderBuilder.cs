using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Defines a builder for constructing service providers with Needlr's auto-discovery capabilities.
/// Coordinates assembly loading, type registration, and plugin execution.
/// </summary>
[DoNotAutoRegister]
public interface IServiceProviderBuilder
{
    /// <summary>
    /// Builds a new <see cref="IServiceProvider"/> using the discovered assemblies and a custom registration callback.
    /// </summary>
    /// <param name="config">The configuration to use for settings.</param>
    /// <returns>The built <see cref="IServiceProvider"/>.</returns>
    IServiceProvider Build(
        IConfiguration config);

    /// <summary>
    /// Builds a new <see cref="IServiceProvider"/> using the discovered assemblies and a custom registration callback.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The configuration to use for settings.</param>
    /// <returns>The built <see cref="IServiceProvider"/>.</returns>
    IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config);

    /// <summary>
    /// Builds a new <see cref="IServiceProvider"/> using the provided <see cref="IServiceCollection"/> and a custom registration callback.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The configuration to use for settings.</param>
    /// <param name="postPluginRegistrationCallbacks">The set of callbacks for additional registration logic</param>
    /// <returns>The built <see cref="IServiceProvider"/>.</returns>
    IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Action<IServiceCollection>> postPluginRegistrationCallbacks);

    /// <summary>
    /// Builds a new <see cref="IServiceProvider"/> with both pre and post registration callbacks.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The configuration to use for settings.</param>
    /// <param name="preRegistrationCallbacks">Callbacks executed before auto-discovery registration (e.g., for open generics).</param>
    /// <param name="postPluginRegistrationCallbacks">Callbacks executed after plugin registration.</param>
    /// <returns>The built <see cref="IServiceProvider"/>.</returns>
    IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Action<IServiceCollection>> preRegistrationCallbacks,
        IReadOnlyList<Action<IServiceCollection>> postPluginRegistrationCallbacks);

    /// <summary>
    /// Configures plugins that require post-build service collection configuration using the built service provider.
    /// </summary>
    /// <param name="provider">The built service provider to use for plugin configuration.</param>
    /// <param name="config">The configuration to use for settings.</param>
    void ConfigurePostBuildServiceCollectionPlugins(
        IServiceProvider provider, 
        IConfiguration config);
    
    /// <summary>
    /// Gets the list of candidate assemblies that will be scanned for service registrations and plugins.
    /// </summary>
    /// <returns>A read-only list of assemblies to be processed for dependency injection.</returns>
    IReadOnlyList<Assembly> GetCandidateAssemblies();
}
