using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.SemanticKernel.PluginScanners;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Extension methods for <see cref="SemanticKernelSyringe"/> providing fluent configuration of Semantic Kernel integration.
/// </summary>
public static class SemanticKernelSyringeExtensions
{
    /// <summary>
    /// Registers a callback to further configure <see cref="KernelFactoryOptions"/> when the
    /// kernel factory is built.
    /// </summary>
    /// <param name="syringe">The Semantic Kernel syringe to configure.</param>
    /// <param name="configure">The configuration callback to apply.</param>
    /// <returns>The configured syringe.</returns>
    public static SemanticKernelSyringe Configure(
        this SemanticKernelSyringe syringe,
        Action<KernelFactoryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe with
        {
            ConfigureKernelFactory = (syringe.ConfigureKernelFactory ?? []).Append(configure).ToList()
        };
    }

    /// <summary>
    /// Adds a single SemanticKernel plugin type to the syringe.
    /// </summary>
    /// <typeparam name="T">The plugin type that contains <see cref="KernelFunctionAttribute"/>-marked methods.</typeparam>
    /// <param name="syringe">The Semantic Kernel syringe to configure.</param>
    /// <returns>The configured syringe.</returns>
    public static SemanticKernelSyringe AddSemanticKernelPlugin<T>(
        this SemanticKernelSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.AddSemanticKernelPlugins([typeof(T)]);
    }

    /// <summary>
    /// Adds SemanticKernel plugins from a pre-discovered list of types.
    /// This is the recommended approach for AOT/trimmed applications.
    /// </summary>
    /// <param name="syringe">The SemanticKernel syringe to configure.</param>
    /// <param name="pluginTypes">
    /// Pre-discovered plugin types, typically from the generated 
    /// <c>NexusLabs.Needlr.Generated.SemanticKernelPlugins.AllPluginTypes</c>.
    /// </param>
    /// <returns>The configured syringe.</returns>
    /// <remarks>
    /// Example usage with source-generated types:
    /// <code>
    /// syringe.AddSemanticKernelPluginsFromGenerated(
    ///     NexusLabs.Needlr.Generated.SemanticKernelPlugins.AllPluginTypes);
    /// </code>
    /// </remarks>
    public static SemanticKernelSyringe AddSemanticKernelPluginsFromGenerated(
        this SemanticKernelSyringe syringe,
        IReadOnlyList<Type> pluginTypes)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(pluginTypes);

        var scanner = new GeneratedSemanticKernelPluginScanner(pluginTypes);
        return syringe.AddSemanticKernelPluginsFromScanner(scanner);
    }

    /// <summary>
    /// Adds SemanticKernel plugins from a custom scanner.
    /// </summary>
    /// <param name="syringe">The SemanticKernel syringe to configure.</param>
    /// <param name="scanner">The scanner to use for plugin discovery.</param>
    /// <param name="includeInstancePlugins">Whether to include instance plugins.</param>
    /// <param name="includeStaticPlugins">Whether to include static plugins.</param>
    /// <returns>The configured syringe.</returns>
    public static SemanticKernelSyringe AddSemanticKernelPluginsFromScanner(
        this SemanticKernelSyringe syringe,
        ISemanticKernelPluginScanner scanner,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(scanner);

        // This method doesn't require reflection for scanning since the scanner provides the types
        // However, the AddSemanticKernelPlugins method still uses reflection for type inspection
        return syringe.AddSemanticKernelPlugins(
            scanner,
            includeInstancePlugins: includeInstancePlugins,
            includeStaticPlugins: includeStaticPlugins);
    }

    /// <summary>
    /// Adds SemanticKernel plugins discovered from all registered assemblies in the service provider.
    /// </summary>
    /// <param name="syringe">The Semantic Kernel syringe to configure.</param>
    /// <param name="includeInstancePlugins">Whether to include instance-based plugins.</param>
    /// <param name="includeStaticPlugins">Whether to include static class plugins.</param>
    /// <returns>The configured syringe.</returns>
    [RequiresUnreferencedCode("Assembly scanning uses reflection to discover types with [KernelFunction] methods.")]
    [RequiresDynamicCode("Assembly scanning uses reflection APIs that may require dynamic code generation.")]
    public static SemanticKernelSyringe AddSemanticKernelPluginsFromAssemblies(
        this SemanticKernelSyringe syringe,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        var assemblies = syringe.ServiceProvider.GetRequiredService<IReadOnlyList<Assembly>>();
        return syringe.AddSemanticKernelPluginsFromAssemblies(
            assemblies,
            includeInstancePlugins: includeInstancePlugins,
            includeStaticPlugins: includeStaticPlugins);
    }

    /// <summary>
    /// Adds SemanticKernel plugins discovered from a specific list of assemblies.
    /// </summary>
    /// <param name="syringe">The Semantic Kernel syringe to configure.</param>
    /// <param name="asssemblies">The assemblies to scan for plugin types.</param>
    /// <param name="includeInstancePlugins">Whether to include instance-based plugins.</param>
    /// <param name="includeStaticPlugins">Whether to include static class plugins.</param>
    /// <returns>The configured syringe.</returns>
    [RequiresUnreferencedCode("Assembly scanning uses reflection to discover types with [KernelFunction] methods.")]
    [RequiresDynamicCode("Assembly scanning uses reflection APIs that may require dynamic code generation.")]
    public static SemanticKernelSyringe AddSemanticKernelPluginsFromAssemblies(
        this SemanticKernelSyringe syringe,
        IReadOnlyList<Assembly> asssemblies,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(asssemblies);

        var scanner = new AssemblySemanticKernelPluginScanner(asssemblies);
        return syringe.AddSemanticKernelPlugins(
            scanner,
            includeInstancePlugins: includeInstancePlugins,
            includeStaticPlugins: includeStaticPlugins);
    }

    /// <summary>
    /// Adds SemanticKernel plugins discovered by scanning all services registered in the service provider.
    /// </summary>
    /// <param name="syringe">The Semantic Kernel syringe to configure.</param>
    /// <returns>The configured syringe.</returns>
    [RequiresUnreferencedCode("Service provider scanning uses reflection to discover types with [KernelFunction] methods.")]
    [RequiresDynamicCode("Service provider scanning uses reflection APIs that may require dynamic code generation.")]
    public static SemanticKernelSyringe AddSemanticKernelPluginsFromProvider(
        this SemanticKernelSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        var scanner = new ServiceProviderSemanticKernelPluginScanner(syringe.ServiceProvider);
        return syringe.AddSemanticKernelPlugins(scanner);
    }

    /// <summary>
    /// Adds SemanticKernel plugins by scanning with a custom <see cref="ISemanticKernelPluginScanner"/>.
    /// </summary>
    /// <param name="syringe">The Semantic Kernel syringe to configure.</param>
    /// <param name="scanner">The scanner that produces plugin types.</param>
    /// <param name="includeInstancePlugins">Whether to include instance-based plugins.</param>
    /// <param name="includeStaticPlugins">Whether to include static class plugins.</param>
    /// <returns>The configured syringe.</returns>
    [RequiresUnreferencedCode("Plugin scanning uses reflection to discover types with [KernelFunction] methods.")]
    [RequiresDynamicCode("Plugin scanning uses reflection APIs that may require dynamic code generation.")]
    public static SemanticKernelSyringe AddSemanticKernelPlugins(
        this SemanticKernelSyringe syringe,
        ISemanticKernelPluginScanner scanner,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(scanner);

        var pluginTypes = scanner.ScanForPluginTypes();
        return syringe.AddSemanticKernelPlugins(
            pluginTypes,
            includeInstancePlugins: includeInstancePlugins,
            includeStaticPlugins: includeStaticPlugins);
    }

    /// <summary>
    /// Adds SemanticKernel plugins from an explicit list of plugin types.
    /// </summary>
    /// <param name="syringe">The Semantic Kernel syringe to configure.</param>
    /// <param name="pluginTypes">The plugin types to register.</param>
    /// <param name="includeInstancePlugins">Whether to include instance-based plugins.</param>
    /// <param name="includeStaticPlugins">Whether to include static class plugins.</param>
    /// <returns>The configured syringe.</returns>
    [RequiresUnreferencedCode("Plugin type inspection uses reflection to check for [KernelFunction] methods.")]
    [RequiresDynamicCode("Plugin type inspection uses reflection APIs that may require dynamic code generation.")]
    public static SemanticKernelSyringe AddSemanticKernelPlugins(
        this SemanticKernelSyringe syringe,
        IReadOnlyList<Type> pluginTypes,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(pluginTypes);

        List<Type> typesToAdd = [];

        foreach (var pluginType in pluginTypes)
        {
            if (pluginType.IsStatic())
            {
                if (!includeStaticPlugins)
                {
                    continue;
                }

                if (!pluginType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Any(m => m.IsDefined(typeof(KernelFunctionAttribute), inherit: true)))
                {
                    continue;
                }

                typesToAdd.Add(pluginType);
                continue;
            }

            if (!includeInstancePlugins)
            {
                continue;
            }

            typesToAdd.Add(pluginType);
        }

        return syringe with
        {
            PluginTypes = (syringe.PluginTypes ?? []).Concat(typesToAdd).Distinct().ToList()
        };
    }
}