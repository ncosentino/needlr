using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public sealed class ServiceCollectionPopulator : IServiceCollectionPopulator
{
    private readonly ITypeRegistrar _typeRegistrar;
    private readonly ITypeFilterer _typeFilterer;
    private readonly PluginFactory _pluginFactory;

    public ServiceCollectionPopulator(
        ITypeRegistrar typeRegistrar,
        ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeRegistrar);
        ArgumentNullException.ThrowIfNull(typeFilterer);

        _typeRegistrar = typeRegistrar;
        _typeFilterer = typeFilterer;
        _pluginFactory = new PluginFactory();
    }

    public IServiceCollection RegisterToServiceCollection(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Assembly> candidateAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(candidateAssemblies);

        services.AddSingleton(typeof(Lazy<>), typeof(LazyFactory<>));

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
            candidateAssemblies);

        foreach (var serviceCollectionPlugin in _pluginFactory.CreatePluginsFromAssemblies<IServiceCollectionPlugin>(candidateAssemblies))
        {
            serviceCollectionPlugin.Configure(options);
        }

        return services;
    }

    private sealed class LazyFactory<T> : Lazy<T>
        where T : notnull
    {
        public LazyFactory(IServiceProvider provider)
            : base(() => provider.GetRequiredService<T>()) { }
    }
}