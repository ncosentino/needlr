using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.SourceGen;

/// <summary>
/// Builds and configures an <see cref="IServiceCollection"/> using source-generated plugin discovery.
/// </summary>
/// <remarks>
/// This builder uses <see cref="GeneratedPluginFactory"/> for plugin discovery and is AOT compatible.
/// </remarks>
[DoNotAutoRegister]
public sealed class GeneratedServiceProviderBuilder : IServiceProviderBuilder
{
    private readonly IServiceCollectionPopulator _serviceCollectionPopulator;
    private readonly Lazy<IReadOnlyList<Assembly>> _lazyCandidateAssemblies;
    private readonly GeneratedPluginFactory _pluginFactory;

    public GeneratedServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider) :
        this(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies: [],
            pluginTypeProvider)
    {
    }

    public GeneratedServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider,
        IReadOnlyList<Assembly> additionalAssemblies,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceCollectionPopulator);
        ArgumentNullException.ThrowIfNull(assemblyProvider);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);
        ArgumentNullException.ThrowIfNull(pluginTypeProvider);

        _serviceCollectionPopulator = serviceCollectionPopulator;
        _pluginFactory = new GeneratedPluginFactory(pluginTypeProvider, allowAllWhenAssembliesEmpty: true);
        _lazyCandidateAssemblies = new(() =>
        {
            var staticAssemblies = assemblyProvider.GetCandidateAssemblies();
            HashSet<string> uniqueAssemblyNames = new(StringComparer.OrdinalIgnoreCase);
            List<Assembly> allCandidateAssemblies = new(additionalAssemblies.Count + staticAssemblies.Count);

            foreach (var assembly in staticAssemblies)
            {
                var name = assembly.FullName ?? assembly.GetName().Name ?? string.Empty;
                if (uniqueAssemblyNames.Add(name))
                {
                    allCandidateAssemblies.Add(assembly);
                }
            }

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
    public void ConfigurePostBuildServiceCollectionPlugins(
        IServiceProvider provider,
        IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(config);

        var candidateAssemblies = GetCandidateAssemblies();
        PostBuildServiceCollectionPluginOptions options = new(
            provider,
            config,
            candidateAssemblies);

        foreach (var plugin in _pluginFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(candidateAssemblies))
        {
            plugin.Configure(options);
        }
    }
}
