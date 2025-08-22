using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.SemanticKernel.PluginScanners;

using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel;

public static class KernelFactoryOptionExtensions
{
    public static KernelFactoryOptions AddSemanticKernelPlugin<T>(
        this KernelFactoryOptions opts)
    {
        opts.KernelBuilder.Plugins.AddFromType<T>();
        return opts;
    }

    public static KernelFactoryOptions AddSemanticKernelPluginsFromAssemblies(
        this KernelFactoryOptions opts,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        var assemblies = opts.ServiceProvider.GetRequiredService<IReadOnlyList<Assembly>>();
        return opts.AddSemanticKernelPluginsFromAssemblies(
            assemblies,
            includeInstancePlugins: includeInstancePlugins,
            includeStaticPlugins: includeStaticPlugins);
    }

    public static KernelFactoryOptions AddSemanticKernelPluginsFromAssemblies(
        this KernelFactoryOptions opts,
        IReadOnlyList<Assembly> asssemblies,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        var scanner = new AssemblySemanticKernelPluginScanner(asssemblies);
        return opts.AddSemanticKernelPlugins(
            scanner,
            includeInstancePlugins: includeInstancePlugins,
            includeStaticPlugins: includeStaticPlugins);
    }

    public static KernelFactoryOptions AddSemanticKernelPluginsFromProvider(
        this KernelFactoryOptions opts)
    {
        var scanner = new ServiceProviderSemanticKernelPluginScanner(opts.ServiceProvider);
        return opts.AddSemanticKernelPlugins(scanner);
    }

    public static KernelFactoryOptions AddSemanticKernelPlugins(
        this KernelFactoryOptions opts,
        ISemanticKernelPluginScanner scanner,
        bool includeInstancePlugins = true,
        bool includeStaticPlugins = true)
    {
        var pluginTypes = scanner.ScanForPluginTypes();
        foreach (var pluginType in pluginTypes)
        {
            if (pluginType.IsStatic())
            {
                if (!includeStaticPlugins)
                {
                    continue;
                }

                var funcs = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.IsDefined(typeof(KernelFunctionAttribute), inherit: true))
                    .Select(m => KernelFunctionFactory.CreateFromMethod(m, target: null))
                    .ToList();

                if (funcs.Count > 0)
                {
                    var plugin = KernelPluginFactory.CreateFromFunctions(pluginType.Name, funcs);
                    opts.KernelBuilder.Plugins.Add(plugin);
                }

                continue;
            }
            
            if (!includeInstancePlugins)
            {
                continue;
            }

            opts.KernelBuilder.Services.TryAddSingleton(serviceProvider =>
            {
                var plugin = KernelPluginFactory.CreateFromType(
                    pluginType,
                    serviceProvider: serviceProvider);
                    return plugin;
            });
        }

        return opts;
    }
}
