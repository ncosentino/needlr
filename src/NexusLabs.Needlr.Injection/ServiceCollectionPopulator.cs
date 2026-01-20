using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public sealed class ServiceCollectionPopulator : IServiceCollectionPopulator
{
    private readonly ITypeRegistrar _typeRegistrar;
    private readonly ITypeFilterer _typeFilterer;
    private readonly IPluginFactory _pluginFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceCollectionPopulator"/> class.
    /// </summary>
    /// <param name="typeRegistrar">The type registrar to use for service registration.</param>
    /// <param name="typeFilterer">The type filterer to determine service lifetimes.</param>
    /// <param name="pluginFactory">The plugin factory to use for plugin discovery and instantiation.</param>
    public ServiceCollectionPopulator(
        ITypeRegistrar typeRegistrar,
        ITypeFilterer typeFilterer,
        IPluginFactory pluginFactory)
    {
        ArgumentNullException.ThrowIfNull(typeRegistrar);
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(pluginFactory);

        _typeRegistrar = typeRegistrar;
        _typeFilterer = typeFilterer;
        _pluginFactory = pluginFactory;
    }

    public IServiceCollection RegisterToServiceCollection(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Assembly> candidateAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(candidateAssemblies);

        services.AddSingleton(services);
        services.AddSingleton(provider => provider);
        services.AddSingleton(typeof(Lazy<>), typeof(LazyFactory<>));
        services.AddSingleton(typeof(IReadOnlyList<>), typeof(ReadOnlyListFactory<>));
        services.AddSingleton(typeof(IReadOnlyCollection<>), typeof(ReadOnlyListFactory<>));
        services.AddSingleton(_pluginFactory);

        foreach (var assembly in candidateAssemblies)
        {
            services.AddSingleton(assembly);
        }

        _typeRegistrar.RegisterTypesFromAssemblies(
            services,
            _typeFilterer,
            candidateAssemblies);

        ServiceCollectionPluginOptions options = new(
            services,
            config,
            candidateAssemblies,
            _pluginFactory);

        foreach (var serviceCollectionPlugin in _pluginFactory.CreatePluginsFromAssemblies<IServiceCollectionPlugin>(candidateAssemblies))
        {
            serviceCollectionPlugin.Configure(options);
        }

        return services;
    }

    private sealed class LazyFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(IServiceProvider provider) :
        Lazy<T>(() => provider.GetRequiredService<T>())
        where T : notnull
    {
    }

    private sealed class ReadOnlyListFactory<T>(IServiceProvider provider) : IReadOnlyList<T>
        where T : notnull
    {
        private readonly Lazy<IReadOnlyList<T>> _lazyItems = new(() => provider
            .GetServices<T>()
            .ToArray());

        public T this[int index] => _lazyItems.Value[index];
        
        public int Count => _lazyItems.Value.Count;
        
        public IEnumerator<T> GetEnumerator() => _lazyItems.Value.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}