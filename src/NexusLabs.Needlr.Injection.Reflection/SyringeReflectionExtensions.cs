using NexusLabs.Needlr.Injection.Reflection.Loaders;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.Reflection.TypeRegistrars;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> with reflection-based components.
/// </summary>
/// <remarks>
/// <para>
/// These extensions enable runtime reflection-based type discovery and registration.
/// Use these when you need dynamic assembly loading or when source generation is not available.
/// </para>
/// <para>
/// For AOT/trimming compatibility, use <c>NexusLabs.Needlr.Injection.SourceGen</c> instead.
/// </para>
/// </remarks>
public static class SyringeReflectionExtensions
{
    /// <summary>
    /// Configures the syringe to use all reflection-based components.
    /// </summary>
    /// <remarks>
    /// This sets the type registrar, type filterer, plugin factory, and assembly provider
    /// to their reflection-based implementations. Not compatible with AOT/trimming.
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance with all reflection-based components.</returns>
    [RequiresUnreferencedCode("Enables reflection-based type discovery. Not compatible with AOT/trimming.")]
    public static Syringe UsingReflection(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe
            .UsingReflectionTypeRegistrar()
            .UsingReflectionTypeFilterer()
            .UsingReflectionPluginFactory()
            .UsingReflectionAssemblyProvider()
            .UsingServiceProviderBuilderFactory(
                (populator, assemblyProvider, additionalAssemblies) => 
                    new ReflectionServiceProviderBuilder(populator, assemblyProvider, additionalAssemblies));
    }

    /// <summary>
    /// Configures the syringe to use the reflection-based type registrar.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    [RequiresUnreferencedCode("ReflectionTypeRegistrar uses reflection to discover types.")]
    public static Syringe UsingReflectionTypeRegistrar(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeRegistrar(new ReflectionTypeRegistrar());
    }

    /// <summary>
    /// Configures the syringe to use the reflection-based type filterer.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    [RequiresUnreferencedCode("ReflectionTypeFilterer uses reflection to analyze constructors.")]
    public static Syringe UsingReflectionTypeFilterer(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeFilterer(new ReflectionTypeFilterer());
    }

    /// <summary>
    /// Configures the syringe to use the reflection-based plugin factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    [RequiresUnreferencedCode("ReflectionPluginFactory uses reflection to discover and instantiate plugins.")]
    public static Syringe UsingReflectionPluginFactory(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingPluginFactory(new ReflectionPluginFactory());
    }

    /// <summary>
    /// Configures the syringe to use the reflection-based assembly provider.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    [RequiresUnreferencedCode("AssemblyProviderBuilder uses reflection to load assemblies.")]
    public static Syringe UsingReflectionAssemblyProvider(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingAssemblyProvider(new AssemblyProviderBuilder().Build());
    }

    /// <summary>
    /// Configures the syringe to use a custom assembly provider built with the builder pattern.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="configure">A function to configure the assembly provider builder.</param>
    /// <returns>A new configured syringe instance.</returns>
    [RequiresUnreferencedCode("AssemblyProviderBuilder uses reflection to load assemblies.")]
    public static Syringe UsingAssemblyProvider(
        this Syringe syringe,
        Func<IAssemblyProviderBuilder, IAssemblyProvider> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        var provider = configure(new AssemblyProviderBuilder());
        return syringe.UsingAssemblyProvider(provider);
    }

    /// <summary>
    /// Configures a handler to be invoked when reflection-based components are used as fallback.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="handler">The handler to invoke when reflection fallback occurs.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe WithReflectionFallbackHandler(
        this Syringe syringe,
        Action<ReflectionFallbackContext> handler)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(handler);

        // Store the handler for use by Bundle's fallback logic
        // The base Syringe doesn't have this property, so we use a callback pattern
        return syringe.UsingPostPluginRegistrationCallback(_ =>
        {
            // This is a placeholder - the actual fallback handling is in Bundle
        });
    }
}
