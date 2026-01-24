using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen.Loaders;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;
using NexusLabs.Needlr.Injection.SourceGen.TypeRegistrars;

namespace NexusLabs.Needlr.Injection.SourceGen;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> with source-generated components.
/// </summary>
/// <remarks>
/// <para>
/// These extensions enable AOT-compatible, zero-reflection type discovery and registration
/// using compile-time generated type registries.
/// </para>
/// <para>
/// To use these extensions, your assembly must have:
/// <list type="bullet">
/// <item>A reference to <c>NexusLabs.Needlr.Generators</c></item>
/// <item>The <c>[assembly: GenerateTypeRegistry(...)]</c> attribute</item>
/// </list>
/// </para>
/// <para>
/// For assembly ordering, use <c>SyringeExtensions.OrderAssemblies</c> after calling <c>UsingSourceGen()</c>.
/// </para>
/// </remarks>
public static class SyringeSourceGenExtensions
{
    /// <summary>
    /// Configures the syringe to use source-generated components from the module initializer bootstrap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses the type providers registered via <see cref="NeedlrSourceGenBootstrap"/>.
    /// The bootstrap is automatically registered by the generated module initializer when you use
    /// <c>[assembly: GenerateTypeRegistry(...)]</c>.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A configured syringe ready for further configuration and building.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no source-generated type providers are registered via NeedlrSourceGenBootstrap.
    /// </exception>
    public static ConfiguredSyringe UsingSourceGen(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        if (!NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out var pluginTypeProvider))
        {
            throw new InvalidOperationException(
                "No source-generated type providers found. Ensure your assembly has " +
                "[assembly: GenerateTypeRegistry(...)] and references NexusLabs.Needlr.Generators.");
        }

        return new ConfiguredSyringe(syringe).UsingGeneratedComponents(injectableTypeProvider, pluginTypeProvider);
    }

    /// <summary>
    /// Configures the syringe with all generated components for zero-reflection operation.
    /// </summary>
    /// <remarks>
    /// This is a strategy method that creates a ConfiguredSyringe. Use this when you have
    /// explicit type providers (e.g., from generated code) rather than using the bootstrap.
    /// </remarks>
    /// <param name="syringe">The base syringe to configure.</param>
    /// <param name="injectableTypeProvider">A function that returns the injectable types.</param>
    /// <param name="pluginTypeProvider">A function that returns the plugin types.</param>
    /// <returns>A configured syringe with all source-generated components.</returns>
    public static ConfiguredSyringe UsingGeneratedComponents(
        this Syringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(injectableTypeProvider);
        ArgumentNullException.ThrowIfNull(pluginTypeProvider);

        return new ConfiguredSyringe(syringe) with
        {
            TypeRegistrar = new GeneratedTypeRegistrar(injectableTypeProvider),
            TypeFilterer = new GeneratedTypeFilterer(injectableTypeProvider),
            PluginFactory = new GeneratedPluginFactory(pluginTypeProvider),
            AssemblyProvider = new GeneratedAssemblyProvider(injectableTypeProvider, pluginTypeProvider),
            ServiceProviderBuilderFactory = (populator, assemblyProvider, _) => 
                new GeneratedServiceProviderBuilder(populator, assemblyProvider, pluginTypeProvider)
        };
    }

    /// <summary>
    /// Configures the syringe with all generated components for zero-reflection operation.
    /// </summary>
    /// <param name="syringe">The configured syringe to update.</param>
    /// <param name="injectableTypeProvider">A function that returns the injectable types.</param>
    /// <param name="pluginTypeProvider">A function that returns the plugin types.</param>
    /// <returns>A configured syringe with all source-generated components.</returns>
    public static ConfiguredSyringe UsingGeneratedComponents(
        this ConfiguredSyringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(injectableTypeProvider);
        ArgumentNullException.ThrowIfNull(pluginTypeProvider);

        return syringe with
        {
            TypeRegistrar = new GeneratedTypeRegistrar(injectableTypeProvider),
            TypeFilterer = new GeneratedTypeFilterer(injectableTypeProvider),
            PluginFactory = new GeneratedPluginFactory(pluginTypeProvider),
            AssemblyProvider = new GeneratedAssemblyProvider(injectableTypeProvider, pluginTypeProvider),
            ServiceProviderBuilderFactory = (populator, assemblyProvider, _) => 
                new GeneratedServiceProviderBuilder(populator, assemblyProvider, pluginTypeProvider)
        };
    }

    /// <summary>
    /// Configures the syringe to use the generated type registrar.
    /// </summary>
    /// <param name="syringe">The configured syringe to update.</param>
    /// <param name="typeProvider">A function that returns the injectable types.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static ConfiguredSyringe UsingGeneratedTypeRegistrar(
        this ConfiguredSyringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> typeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(typeProvider);
        return syringe.UsingTypeRegistrar(new GeneratedTypeRegistrar(typeProvider));
    }

    /// <summary>
    /// Configures the syringe to use the generated type filterer.
    /// </summary>
    /// <param name="syringe">The configured syringe to update.</param>
    /// <param name="typeProvider">A function that returns the injectable types.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static ConfiguredSyringe UsingGeneratedTypeFilterer(
        this ConfiguredSyringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> typeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(typeProvider);
        return syringe.UsingTypeFilterer(new GeneratedTypeFilterer(typeProvider));
    }

    /// <summary>
    /// Configures the syringe to use the generated plugin factory.
    /// </summary>
    /// <param name="syringe">The configured syringe to update.</param>
    /// <param name="pluginProvider">A function that returns the plugin types.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static ConfiguredSyringe UsingGeneratedPluginFactory(
        this ConfiguredSyringe syringe,
        Func<IReadOnlyList<PluginTypeInfo>> pluginProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(pluginProvider);
        return syringe.UsingPluginFactory(new GeneratedPluginFactory(pluginProvider));
    }

    /// <summary>
    /// Configures the syringe to use the generated assembly provider.
    /// </summary>
    /// <param name="syringe">The configured syringe to update.</param>
    /// <param name="injectableTypeProvider">A function that returns the injectable types.</param>
    /// <param name="pluginTypeProvider">A function that returns the plugin types.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static ConfiguredSyringe UsingGeneratedAssemblyProvider(
        this ConfiguredSyringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(injectableTypeProvider);
        ArgumentNullException.ThrowIfNull(pluginTypeProvider);
        return syringe.UsingAssemblyProvider(
            new GeneratedAssemblyProvider(injectableTypeProvider, pluginTypeProvider));
    }
}
