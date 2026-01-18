using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.Injection.Bundle;

/// <summary>
/// Extension methods that provide automatic fallback between source-generated and reflection-based components.
/// </summary>
/// <remarks>
/// <para>
/// When using the Bundle package, Needlr will automatically:
/// <list type="number">
/// <item>Try to use source-generated components if available (via NeedlrSourceGenBootstrap)</item>
/// <item>Fall back to reflection-based components if source generation is not configured</item>
/// </list>
/// </para>
/// <para>
/// Use <see cref="WithFallbackBehavior"/> to configure the fallback behavior, or
/// <see cref="WithFastFailOnReflection"/> to throw if reflection is used.
/// </para>
/// </remarks>
public static class SyringeBundleExtensions
{
    /// <summary>
    /// Configures the syringe with automatic fallback from source-gen to reflection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method automatically detects whether source generation is available
    /// and configures the appropriate components. If source-generated providers
    /// are registered via NeedlrSourceGenBootstrap, they will be used. Otherwise,
    /// reflection-based components are used as fallback.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Reflection components are only used when source-gen is not available.")]
    public static Syringe UsingAutoConfiguration(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        // Always set the service provider builder factory - it handles the source-gen vs reflection detection
        var result = syringe.UsingServiceProviderBuilderFactory(
            (populator, assemblyProvider, additionalAssemblies) => 
                new ServiceProviderBuilder(populator, assemblyProvider, additionalAssemblies));

        if (NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out var pluginTypeProvider))
        {
            return result.UsingGeneratedComponents(injectableTypeProvider, pluginTypeProvider);
        }

        return result.UsingReflection();
    }

    /// <summary>
    /// Configures the syringe with automatic fallback and a custom fallback handler.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="onFallback">Called when reflection fallback occurs. Can be used for logging or to throw.</param>
    /// <returns>A new configured syringe instance.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Reflection components are only used when source-gen is not available.")]
    public static Syringe WithFallbackBehavior(
        this Syringe syringe,
        Action<ReflectionFallbackContext>? onFallback)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        // Always set the service provider builder factory
        var result = syringe.UsingServiceProviderBuilderFactory(
            (populator, assemblyProvider, additionalAssemblies) => 
                new ServiceProviderBuilder(populator, assemblyProvider, additionalAssemblies));

        if (NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out var pluginTypeProvider))
        {
            return result.UsingGeneratedComponents(injectableTypeProvider, pluginTypeProvider);
        }

        // Invoke fallback handler if provided
        if (onFallback is not null)
        {
            onFallback(ReflectionFallbackHandlers.CreateTypeRegistrarContext());
        }

        return result.UsingReflection();
    }

    /// <summary>
    /// Configures the syringe to throw an exception if reflection fallback would occur.
    /// </summary>
    /// <remarks>
    /// Use this in AOT/trimming scenarios to ensure that source-generated components are always used.
    /// If no source-generated providers are registered, an <see cref="InvalidOperationException"/> is thrown.
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe WithFastFailOnReflection(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.WithFallbackBehavior(ReflectionFallbackHandlers.ThrowException);
    }

    /// <summary>
    /// Configures the syringe to log warnings when reflection fallback occurs.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Reflection components are only used when source-gen is not available.")]
    public static Syringe WithFallbackLogging(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.WithFallbackBehavior(ReflectionFallbackHandlers.LogWarning);
    }
}
