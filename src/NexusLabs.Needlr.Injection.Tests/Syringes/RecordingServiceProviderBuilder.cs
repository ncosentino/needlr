using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Service provider builder that records what <see cref="ConfiguredSyringe"/> handed it and
/// executes the callbacks in the documented order before building a real service provider.
/// </summary>
[DoNotAutoRegister]
public sealed class RecordingServiceProviderBuilder : IServiceProviderBuilder
{
    private readonly IServiceCollectionPopulator _populator;
    private readonly IAssemblyProvider _assemblyProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingServiceProviderBuilder"/> class.
    /// </summary>
    /// <param name="populator">The populator supplied by the syringe.</param>
    /// <param name="assemblyProvider">The assembly provider supplied by the syringe.</param>
    /// <param name="additionalAssemblies">The additional assemblies supplied by the syringe.</param>
    public RecordingServiceProviderBuilder(
        IServiceCollectionPopulator populator,
        IAssemblyProvider assemblyProvider,
        IReadOnlyList<Assembly> additionalAssemblies)
    {
        _populator = populator;
        _assemblyProvider = assemblyProvider;
        AdditionalAssemblies = additionalAssemblies;
    }

    /// <summary>
    /// Gets the populator supplied by the syringe.
    /// </summary>
    public IServiceCollectionPopulator Populator => _populator;

    /// <summary>
    /// Gets the assembly provider supplied by the syringe.
    /// </summary>
    public IAssemblyProvider AssemblyProvider => _assemblyProvider;

    /// <summary>
    /// Gets the additional assemblies supplied by the syringe.
    /// </summary>
    public IReadOnlyList<Assembly> AdditionalAssemblies { get; }

    /// <summary>
    /// Gets the configuration supplied to the last build call.
    /// </summary>
    public IConfiguration? ObservedConfiguration { get; private set; }

    /// <summary>
    /// Gets the pre-registration callbacks supplied to the last build call.
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection>> ObservedPreRegistrationCallbacks { get; private set; } = [];

    /// <summary>
    /// Gets the post-plugin registration callbacks supplied to the last build call.
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection>> ObservedPostPluginRegistrationCallbacks { get; private set; } = [];

    /// <inheritdoc />
    public IServiceProvider Build(IConfiguration config)
    {
        return Build(new ServiceCollection(), config, [], []);
    }

    /// <inheritdoc />
    public IServiceProvider Build(IServiceCollection services, IConfiguration config)
    {
        return Build(services, config, [], []);
    }

    /// <inheritdoc />
    public IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Action<IServiceCollection>> postPluginRegistrationCallbacks)
    {
        return Build(services, config, [], postPluginRegistrationCallbacks);
    }

    /// <inheritdoc />
    public IServiceProvider Build(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Action<IServiceCollection>> preRegistrationCallbacks,
        IReadOnlyList<Action<IServiceCollection>> postPluginRegistrationCallbacks)
    {
        ObservedConfiguration = config;
        ObservedPreRegistrationCallbacks = preRegistrationCallbacks;
        ObservedPostPluginRegistrationCallbacks = postPluginRegistrationCallbacks;

        foreach (var callback in preRegistrationCallbacks)
        {
            callback(services);
        }

        services.AddSingleton(config);
        _populator.RegisterToServiceCollection(services, config, _assemblyProvider.GetCandidateAssemblies());

        foreach (var callback in postPluginRegistrationCallbacks)
        {
            callback(services);
        }

        return services.BuildServiceProvider();
    }

    /// <inheritdoc />
    public void ConfigurePostBuildServiceCollectionPlugins(IServiceProvider provider, IConfiguration config)
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<Assembly> GetCandidateAssemblies()
    {
        return [.. _assemblyProvider.GetCandidateAssemblies(), .. AdditionalAssemblies];
    }
}
