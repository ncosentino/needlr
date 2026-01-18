using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.SemanticKernel.PluginScanners;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel;

public static class SemanticKernelSyringeExtensions
{
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

    public static SemanticKernelSyringe AddSemanticKernelPlugin<T>(
        this SemanticKernelSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.AddSemanticKernelPlugins([typeof(T)]);
    }

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

    [RequiresUnreferencedCode("Service provider scanning uses reflection to discover types with [KernelFunction] methods.")]
    [RequiresDynamicCode("Service provider scanning uses reflection APIs that may require dynamic code generation.")]
    public static SemanticKernelSyringe AddSemanticKernelPluginsFromProvider(
        this SemanticKernelSyringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        var scanner = new ServiceProviderSemanticKernelPluginScanner(syringe.ServiceProvider);
        return syringe.AddSemanticKernelPlugins(scanner);
    }

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