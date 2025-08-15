using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

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
            HashSet<string> uniqueAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);
            List<Assembly> allCandidateAssemblies = new(additionalAssemblies.Count + staticAssemblies.Count);

            // load the static referenced assemblies
            foreach (var assembly in staticAssemblies)
            {
                if (uniqueAssemblyPaths.Add(assembly.Location))
                {
                    allCandidateAssemblies.Add(assembly);
                }
            }

            // load any additional
            foreach (var assembly in additionalAssemblies)
            {
                if (uniqueAssemblyPaths.Add(assembly.Location))
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
        PluginFactory pluginFactory = new();
        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(candidateAssemblies))
        {
            plugin.Configure(options);
        }
    }
}
