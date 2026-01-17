using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Provides a fluent API for configuring and building service providers using Needlr.
/// Acts as an immutable container that creates copies via extension methods.
/// </summary>
[DoNotAutoRegister]
public sealed record Syringe
{
    internal ITypeRegistrar? TypeRegistrar { get; init; }
    internal ITypeFilterer? TypeFilterer { get; init; }
    internal IPluginFactory? PluginFactory { get; init; }
    internal Func<ITypeRegistrar, ITypeFilterer, IPluginFactory, IServiceCollectionPopulator>? ServiceCollectionPopulatorFactory { get; init; }
    internal IAssemblyProvider? AssemblyProvider { get; init; }
    internal IReadOnlyList<Assembly>? AdditionalAssemblies { get; init; }
    internal IReadOnlyList<Action<IServiceCollection>>? PostPluginRegistrationCallbacks { get; init; }

    /// <summary>
    /// Builds a service provider with the configured settings.
    /// </summary>
    /// <param name="config">The configuration to use for building the service provider.</param>
    /// <returns>The configured <see cref="IServiceProvider"/>.</returns>
    public IServiceProvider BuildServiceProvider(
        IConfiguration config)
    {
        var typeRegistrar = GetOrCreateTypeRegistrar();
        var typeFilterer = GetOrCreateTypeFilterer();
        var pluginFactory = GetOrCreatePluginFactory();
        var serviceCollectionPopulator = GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory);
        var assemblyProvider = GetOrCreateAssemblyProvider();
        var additionalAssemblies = AdditionalAssemblies ?? [];
        var callbacks = PostPluginRegistrationCallbacks ?? [];

        var serviceProviderBuilder = new ServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies);

        return serviceProviderBuilder.Build(
            services: new ServiceCollection(),
            config: config,
            postPluginRegistrationCallbacks: callbacks);
    }
        
    /// <summary>
    /// Gets the configured type registrar or creates a default one.
    /// </summary>
    public ITypeRegistrar GetOrCreateTypeRegistrar()
    {
        return TypeRegistrar ?? new TypeRegistrars.DefaultTypeRegistrar();
    }

    /// <summary>
    /// Gets the configured type filterer or creates a default one.
    /// </summary>
    public ITypeFilterer GetOrCreateTypeFilterer()
    {
        return TypeFilterer ?? new TypeFilterers.DefaultTypeFilterer();
    }

    /// <summary>
    /// Gets the configured plugin factory or creates a default one.
    /// </summary>
    public IPluginFactory GetOrCreatePluginFactory()
    {
        return PluginFactory ?? new NexusLabs.Needlr.PluginFactory();
    }

    /// <summary>
    /// Gets the configured service collection populator or creates a default one.
    /// </summary>
    public IServiceCollectionPopulator GetOrCreateServiceCollectionPopulator(
        ITypeRegistrar typeRegistrar,
        ITypeFilterer typeFilterer,
        IPluginFactory pluginFactory)
    {
        return ServiceCollectionPopulatorFactory?.Invoke(typeRegistrar, typeFilterer, pluginFactory)
            ?? new ServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory);
    }

    /// <summary>
    /// Gets the configured assembly provider or creates a default one.
    /// </summary>
    public IAssemblyProvider GetOrCreateAssemblyProvider()
    {
        return AssemblyProvider ?? new AssembyProviderBuilder().Build();
    }

    /// <summary>
    /// Gets the configured additional assemblies.
    /// </summary>
    public IReadOnlyList<Assembly> GetAdditionalAssemblies()
    {
        return AdditionalAssemblies ?? [];
    }

    /// <summary>
    /// Gets the configured post-plugin registration callbacks.
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection>> GetPostPluginRegistrationCallbacks()
    {
        return PostPluginRegistrationCallbacks ?? [];
    }
}