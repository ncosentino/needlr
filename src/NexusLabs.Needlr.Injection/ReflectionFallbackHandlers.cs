using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.PluginFactories;
using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Provides built-in handlers for reflection fallback scenarios.
/// Use these with <see cref="SyringeExtensions.WithReflectionFallbackHandler"/> to control
/// what happens when source-generated components are not available.
/// </summary>
public static class ReflectionFallbackHandlers
{
    /// <summary>
    /// A handler that throws an <see cref="InvalidOperationException"/> when reflection fallback occurs.
    /// Use this to enforce source-generation in AOT/trimmed applications.
    /// </summary>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .WithReflectionFallbackHandler(ReflectionFallbackHandlers.ThrowException);
    /// </code>
    /// </example>
    public static void ThrowException(ReflectionFallbackContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        throw new InvalidOperationException(
            $"Reflection fallback detected for {context.ComponentName}. " +
            $"{context.Reason} " +
            $"Expected: {context.GeneratedComponentType.Name}, " +
            $"Fallback: {context.ReflectionComponentType.Name}. " +
            "Add [assembly: GenerateTypeRegistry(...)] to enable source generation, " +
            "or call .UsingReflection() to explicitly opt into reflection-based discovery.");
    }

    /// <summary>
    /// A handler that writes a warning to <see cref="Console.Error"/> when reflection fallback occurs.
    /// Useful for development/debugging to identify missing source generation.
    /// </summary>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .WithReflectionFallbackHandler(ReflectionFallbackHandlers.LogWarning);
    /// </code>
    /// </example>
    public static void LogWarning(ReflectionFallbackContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Console.Error.WriteLine(
            $"[Needlr Warning] Reflection fallback for {context.ComponentName}: {context.Reason}");
    }

    /// <summary>
    /// A handler that does nothing when reflection fallback occurs.
    /// This is the default behavior - silent fallback to reflection.
    /// </summary>
    public static void Silent(ReflectionFallbackContext context)
    {
        // Intentionally empty - silent fallback
    }

    /// <summary>
    /// Creates context for TypeRegistrar fallback.
    /// </summary>
    public static ReflectionFallbackContext CreateTypeRegistrarContext() => new()
    {
        ComponentName = "TypeRegistrar",
        Reason = "No source-generated TypeRegistry found via NeedlrSourceGenBootstrap.",
        ReflectionComponentType = typeof(DefaultTypeRegistrar),
        GeneratedComponentType = typeof(GeneratedTypeRegistrar)
    };

    /// <summary>
    /// Creates context for TypeFilterer fallback.
    /// </summary>
    public static ReflectionFallbackContext CreateTypeFiltererContext() => new()
    {
        ComponentName = "TypeFilterer",
        Reason = "No source-generated TypeRegistry found via NeedlrSourceGenBootstrap.",
        ReflectionComponentType = typeof(DefaultTypeFilterer),
        GeneratedComponentType = typeof(GeneratedTypeFilterer)
    };

    /// <summary>
    /// Creates context for PluginFactory fallback.
    /// </summary>
    public static ReflectionFallbackContext CreatePluginFactoryContext() => new()
    {
        ComponentName = "PluginFactory",
        Reason = "No source-generated TypeRegistry found via NeedlrSourceGenBootstrap.",
        ReflectionComponentType = typeof(NexusLabs.Needlr.PluginFactory),
        GeneratedComponentType = typeof(GeneratedPluginFactory)
    };

    /// <summary>
    /// Creates context for AssemblyProvider fallback.
    /// </summary>
    public static ReflectionFallbackContext CreateAssemblyProviderContext() => new()
    {
        ComponentName = "AssemblyProvider",
        Reason = "No source-generated TypeRegistry found via NeedlrSourceGenBootstrap.",
        ReflectionComponentType = typeof(AssembyProviderBuilder),
        GeneratedComponentType = typeof(GeneratedAssemblyProvider)
    };
}
