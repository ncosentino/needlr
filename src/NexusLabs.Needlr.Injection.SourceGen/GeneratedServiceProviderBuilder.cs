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
    private readonly IAssemblyProvider _assemblyProvider;
    private readonly GeneratedPluginFactory _pluginFactory;

    public GeneratedServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceCollectionPopulator);
        ArgumentNullException.ThrowIfNull(assemblyProvider);
        ArgumentNullException.ThrowIfNull(pluginTypeProvider);

        _serviceCollectionPopulator = serviceCollectionPopulator;
        _assemblyProvider = assemblyProvider;
        _pluginFactory = new GeneratedPluginFactory(pluginTypeProvider, allowAllWhenAssembliesEmpty: true);
    }

    public IReadOnlyList<Assembly> GetCandidateAssemblies() =>
        _assemblyProvider.GetCandidateAssemblies();

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

        foreach (var plugin in _pluginFactory.CreatePlugins<IPostBuildServiceCollectionPlugin>())
        {
            plugin.Configure(options);
        }
    }
}
