using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.Bundle;

/// <summary>
/// Builds and configures an <see cref="IServiceCollection"/>, scanning assemblies for injectable types and plugins.
/// </summary>
[DoNotAutoRegister]
public sealed class ServiceProviderBuilder : IServiceProviderBuilder
{
    private readonly IServiceCollectionPopulator _serviceCollectionPopulator;
    private readonly Lazy<IReadOnlyList<Assembly>> _lazyCandidateAssemblies;

    public ServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider) :
        this(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies: [])
    {
    }

    public ServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider,
        IReadOnlyList<Assembly> additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(serviceCollectionPopulator);
        ArgumentNullException.ThrowIfNull(assemblyProvider);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        _serviceCollectionPopulator = serviceCollectionPopulator;
        _lazyCandidateAssemblies = new(() =>
        {
            var staticAssemblies = assemblyProvider.GetCandidateAssemblies();
            // Use assembly full name for deduplication instead of Location (Location is empty for single-file/AOT)
            HashSet<string> uniqueAssemblyNames = new(StringComparer.OrdinalIgnoreCase);
            List<Assembly> allCandidateAssemblies = new(additionalAssemblies.Count + staticAssemblies.Count);

            // load the static referenced assemblies
            foreach (var assembly in staticAssemblies)
            {
                var name = assembly.FullName ?? assembly.GetName().Name ?? string.Empty;
                if (uniqueAssemblyNames.Add(name))
                {
                    allCandidateAssemblies.Add(assembly);
                }
            }

            // load any additional
            foreach (var assembly in additionalAssemblies)
            {
                var name = assembly.FullName ?? assembly.GetName().Name ?? string.Empty;
                if (uniqueAssemblyNames.Add(name))
                {
                    allCandidateAssemblies.Add(assembly);
                }
            }

            return allCandidateAssemblies.Distinct().ToArray();
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<Assembly> GetCandidateAssemblies() => _lazyCandidateAssemblies.Value;

    /// <inheritdoc />
    public IServiceProvider Build(
        IConfiguration config) =>
        Build(
            services: new ServiceCollection(),
            config: config);

    /// <inheritdoc />
    public IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config) =>
        Build(
            services: services,
            config: config,
            postPluginRegistrationCallbacks: []);

    /// <inheritdoc/>
    public IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Action<IServiceCollection>> postPluginRegistrationCallbacks)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(postPluginRegistrationCallbacks);

        services.AddSingleton(config);
        _serviceCollectionPopulator.RegisterToServiceCollection(
            services,
            config,
            GetCandidateAssemblies());

        foreach (var callback in postPluginRegistrationCallbacks)
        {
            callback.Invoke(services);
        }

        var provider = services.BuildServiceProvider();

        ConfigurePostBuildServiceCollectionPlugins(provider, config);

        return provider;
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
        Justification = "PluginFactory is only used as fallback when source-gen bootstrap is not present. AOT apps use source-gen.")]
    public void ConfigurePostBuildServiceCollectionPlugins(
        IServiceProvider provider,
        IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(config);

        var candidateAssemblies = GetCandidateAssemblies();

        IPluginFactory pluginFactory = NeedlrSourceGenBootstrap.TryGetProviders(out _, out var pluginTypeProvider)
            ? new GeneratedPluginFactory(pluginTypeProvider, allowAllWhenAssembliesEmpty: true)
            : new ReflectionPluginFactory();

        PostBuildServiceCollectionPluginOptions options = new(
            provider,
            config,
            candidateAssemblies,
            pluginFactory);

        var executedPluginTypes = new HashSet<Type>();
        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(candidateAssemblies))
        {
            var pluginType = plugin.GetType();
            if (!executedPluginTypes.Add(pluginType))
            {
                // Skip duplicate plugin instances of the same type
                continue;
            }

            plugin.Configure(options);
        }
    }
}
