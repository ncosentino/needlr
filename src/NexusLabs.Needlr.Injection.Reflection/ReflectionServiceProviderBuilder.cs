using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.Reflection.PluginFactories;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Builds and configures an <see cref="IServiceCollection"/> using reflection-based plugin discovery.
/// </summary>
/// <remarks>
/// This builder uses <see cref="ReflectionPluginFactory"/> for plugin discovery and is not AOT compatible.
/// </remarks>
[DoNotAutoRegister]
[RequiresUnreferencedCode("ReflectionServiceProviderBuilder uses reflection for plugin discovery.")]
public sealed class ReflectionServiceProviderBuilder : IServiceProviderBuilder
{
    private readonly IServiceCollectionPopulator _serviceCollectionPopulator;
    private readonly Lazy<IReadOnlyList<Assembly>> _lazyCandidateAssemblies;
    private readonly ReflectionPluginFactory _pluginFactory;

    public ReflectionServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider) :
        this(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies: [])
    {
    }

    public ReflectionServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider,
        IReadOnlyList<Assembly> additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(serviceCollectionPopulator);
        ArgumentNullException.ThrowIfNull(assemblyProvider);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        _serviceCollectionPopulator = serviceCollectionPopulator;
        _pluginFactory = new ReflectionPluginFactory();
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
            candidateAssemblies,
            _pluginFactory);

        foreach (var plugin in _pluginFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(candidateAssemblies))
        {
            plugin.Configure(options);
        }
    }
}
