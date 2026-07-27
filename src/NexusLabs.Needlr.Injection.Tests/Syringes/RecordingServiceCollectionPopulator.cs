using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Service collection populator that records the components it was composed from and
/// registers a single resolvable service.
/// </summary>
[DoNotAutoRegister]
public sealed class RecordingServiceCollectionPopulator : IServiceCollectionPopulator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingServiceCollectionPopulator"/> class.
    /// </summary>
    /// <param name="typeRegistrar">The type registrar the factory was given.</param>
    /// <param name="typeFilterer">The type filterer the factory was given.</param>
    /// <param name="pluginFactory">The plugin factory the factory was given.</param>
    public RecordingServiceCollectionPopulator(
        ITypeRegistrar typeRegistrar,
        ITypeFilterer typeFilterer,
        IPluginFactory pluginFactory)
    {
        TypeRegistrar = typeRegistrar;
        TypeFilterer = typeFilterer;
        PluginFactory = pluginFactory;
    }

    /// <summary>
    /// Gets the type registrar the factory was given.
    /// </summary>
    public ITypeRegistrar TypeRegistrar { get; }

    /// <summary>
    /// Gets the type filterer the factory was given.
    /// </summary>
    public ITypeFilterer TypeFilterer { get; }

    /// <summary>
    /// Gets the plugin factory the factory was given.
    /// </summary>
    public IPluginFactory PluginFactory { get; }

    /// <inheritdoc />
    public IServiceCollection RegisterToServiceCollection(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Assembly> candidateAssemblies)
    {
        services.AddSingleton<ISyringeTestService, SyringeTestService>();
        return services;
    }
}
