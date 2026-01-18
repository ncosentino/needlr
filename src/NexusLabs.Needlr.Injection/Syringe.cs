using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.TypeFilterers;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Provides a fluent API for configuring and building service providers using Needlr.
/// Acts as an immutable container that creates copies via extension methods.
/// </summary>
/// <remarks>
/// <para>
/// Syringe requires explicit configuration of its components. Use one of these approaches:
/// </para>
/// <list type="bullet">
/// <item>Reference <c>NexusLabs.Needlr.Injection.SourceGen</c> and call <c>.UsingSourceGen()</c></item>
/// <item>Reference <c>NexusLabs.Needlr.Injection.Reflection</c> and call <c>.UsingReflection()</c></item>
/// <item>Reference <c>NexusLabs.Needlr.Injection.Bundle</c> for automatic fallback behavior</item>
/// </list>
/// </remarks>
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
    /// Factory for creating <see cref="IServiceProviderBuilder"/> instances.
    /// </summary>
    internal Func<IServiceCollectionPopulator, IAssemblyProvider, IReadOnlyList<Assembly>, IServiceProviderBuilder>? ServiceProviderBuilderFactory { get; init; }

    /// <summary>
    /// Builds a service provider with the configured settings.
    /// </summary>
    /// <param name="config">The configuration to use for building the service provider.</param>
    /// <returns>The configured <see cref="IServiceProvider"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if required components (TypeRegistrar, TypeFilterer, PluginFactory, AssemblyProvider) are not configured.
    /// </exception>
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

        var serviceProviderBuilder = GetOrCreateServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies);

        return serviceProviderBuilder.Build(
            services: new ServiceCollection(),
            config: config,
            postPluginRegistrationCallbacks: callbacks);
    }
        
    /// <summary>
    /// Gets the configured type registrar.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no type registrar is configured. Use <c>.UsingSourceGen()</c>, <c>.UsingReflection()</c>,
    /// or reference the Bundle package for automatic fallback.
    /// </exception>
    public ITypeRegistrar GetOrCreateTypeRegistrar()
    {
        return TypeRegistrar ?? throw new InvalidOperationException(
            "No TypeRegistrar configured. Add a reference to NexusLabs.Needlr.Injection.SourceGen and call .UsingSourceGen(), " +
            "or add NexusLabs.Needlr.Injection.Reflection and call .UsingReflection(), " +
            "or add NexusLabs.Needlr.Injection.Bundle for automatic fallback behavior.");
    }

    /// <summary>
    /// Gets the configured type filterer or creates an empty one.
    /// </summary>
    public ITypeFilterer GetOrCreateTypeFilterer()
    {
        return TypeFilterer ?? new EmptyTypeFilterer();
    }

    /// <summary>
    /// Gets the configured plugin factory.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no plugin factory is configured. Use <c>.UsingSourceGen()</c>, <c>.UsingReflection()</c>,
    /// or reference the Bundle package for automatic fallback.
    /// </exception>
    public IPluginFactory GetOrCreatePluginFactory()
    {
        return PluginFactory ?? throw new InvalidOperationException(
            "No PluginFactory configured. Add a reference to NexusLabs.Needlr.Injection.SourceGen and call .UsingSourceGen(), " +
            "or add NexusLabs.Needlr.Injection.Reflection and call .UsingReflection(), " +
            "or add NexusLabs.Needlr.Injection.Bundle for automatic fallback behavior.");
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
    /// Gets the configured service provider builder or throws if not configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no service provider builder factory is configured. Use <c>.UsingSourceGen()</c>, <c>.UsingReflection()</c>,
    /// or reference the Bundle package for automatic fallback.
    /// </exception>
    public IServiceProviderBuilder GetOrCreateServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider,
        IReadOnlyList<Assembly> additionalAssemblies)
    {
        return ServiceProviderBuilderFactory?.Invoke(serviceCollectionPopulator, assemblyProvider, additionalAssemblies)
            ?? throw new InvalidOperationException(
                "No ServiceProviderBuilderFactory configured. Add a reference to NexusLabs.Needlr.Injection.SourceGen and call .UsingSourceGen(), " +
                "or add NexusLabs.Needlr.Injection.Reflection and call .UsingReflection(), " +
                "or add NexusLabs.Needlr.Injection.Bundle for automatic fallback behavior.");
    }

    /// <summary>
    /// Gets the configured assembly provider.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no assembly provider is configured. Use <c>.UsingSourceGen()</c>, <c>.UsingReflection()</c>,
    /// or reference the Bundle package for automatic fallback.
    /// </exception>
    public IAssemblyProvider GetOrCreateAssemblyProvider()
    {
        return AssemblyProvider ?? throw new InvalidOperationException(
            "No AssemblyProvider configured. Add a reference to NexusLabs.Needlr.Injection.SourceGen and call .UsingSourceGen(), " +
            "or add NexusLabs.Needlr.Injection.Reflection and call .UsingReflection(), " +
            "or add NexusLabs.Needlr.Injection.Bundle for automatic fallback behavior.");
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