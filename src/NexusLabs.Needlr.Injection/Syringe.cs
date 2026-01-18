using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.PluginFactories;
using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;

using System.Diagnostics.CodeAnalysis;
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
    internal Action<ReflectionFallbackContext>? ReflectionFallbackHandler { get; init; }

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
    /// Gets the configured type registrar or creates a reflection-based one.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
        Justification = "ReflectionTypeRegistrar is only used as fallback when source-gen bootstrap is not present. AOT apps use source-gen.")]
    public ITypeRegistrar GetOrCreateTypeRegistrar()
    {
        if (TypeRegistrar is not null)
            return TypeRegistrar;

        if (NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out _))
            return new GeneratedTypeRegistrar(injectableTypeProvider);

        ReflectionFallbackHandler?.Invoke(ReflectionFallbackHandlers.CreateTypeRegistrarContext());
        return new ReflectionTypeRegistrar();
    }

    /// <summary>
    /// Gets the configured type filterer or creates a reflection-based one.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
        Justification = "ReflectionTypeFilterer is only used as fallback when source-gen bootstrap is not present. AOT apps use source-gen.")]
    public ITypeFilterer GetOrCreateTypeFilterer()
    {
        if (TypeFilterer is not null)
            return TypeFilterer;

        if (NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out _))
            return new GeneratedTypeFilterer(injectableTypeProvider);

        ReflectionFallbackHandler?.Invoke(ReflectionFallbackHandlers.CreateTypeFiltererContext());
        return new ReflectionTypeFilterer();
    }

    /// <summary>
    /// Gets the configured plugin factory or creates a reflection-based one.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
        Justification = "PluginFactory is only used as fallback when source-gen bootstrap is not present. AOT apps use source-gen.")]
    public IPluginFactory GetOrCreatePluginFactory()
    {
        if (PluginFactory is not null)
            return PluginFactory;

        if (NeedlrSourceGenBootstrap.TryGetProviders(out _, out var pluginTypeProvider))
            return new GeneratedPluginFactory(pluginTypeProvider, allowAllWhenAssembliesEmpty: true);

        ReflectionFallbackHandler?.Invoke(ReflectionFallbackHandlers.CreatePluginFactoryContext());
        return new NexusLabs.Needlr.PluginFactory();
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
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", 
        Justification = "AssembyProviderBuilder is only used as fallback when source-gen bootstrap is not present. AOT apps use source-gen.")]
    public IAssemblyProvider GetOrCreateAssemblyProvider()
    {
        if (AssemblyProvider is not null)
            return AssemblyProvider;

        if (NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out var pluginTypeProvider))
            return new GeneratedAssemblyProvider(injectableTypeProvider, pluginTypeProvider);

        ReflectionFallbackHandler?.Invoke(ReflectionFallbackHandlers.CreateAssemblyProviderContext());
        return new AssembyProviderBuilder().Build();
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