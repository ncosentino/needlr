using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;

using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel;

[DoNotAutoRegister]
public sealed record SemanticKernelSyringe
{
    public required IServiceProvider ServiceProvider { get; init; }

    internal List<Action<KernelFactoryOptions>>? ConfigureKernelFactory { get; init; } = [];

    internal List<Type>? PluginTypes { get; init; } = [];

    public IKernelFactory BuildKernelFactory(
        Action<KernelFactoryOptions>? configure = null)
    {
        var pluginFactory = ServiceProvider.GetRequiredService<IPluginFactory>();
        KernelFactory kernelFactory = new(
            _serviceProvider: ServiceProvider,
            _pluginFactory: pluginFactory,
            _configure: options =>
            {
                SetupPlugins(
                    options.KernelBuilder,
                    PluginTypes ?? []);

                foreach (var callback in ConfigureKernelFactory ?? [])
                {
                    callback?.Invoke(options);
                }

                configure?.Invoke(options);
            });
        return kernelFactory;
    }

    private static void SetupPlugins(
        IKernelBuilder kernelBuilder,
        IReadOnlyList<Type> pluginTypes)
    {
        foreach (var pluginType in pluginTypes)
        {
            if (pluginType.IsStatic())
            {
                var funcs = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.IsDefined(typeof(KernelFunctionAttribute), inherit: true))
                    .Select(m => KernelFunctionFactory.CreateFromMethod(m, target: null))
                    .ToList();

                if (funcs.Count > 0)
                {
                    var plugin = KernelPluginFactory.CreateFromFunctions(pluginType.Name, funcs);
                    kernelBuilder.Plugins.Add(plugin);
                }

                continue;
            }

            kernelBuilder.Plugins.Services.AddSingleton(sp => BuildPluginFromType(pluginType, sp));
        }

        static KernelPlugin BuildPluginFromType(
            Type pluginType,
            IServiceProvider serviceProvider)
        {
            var plugin = KernelPluginFactory.CreateFromType(
                instanceType: pluginType,
                pluginName: pluginType.Name,
                serviceProvider: serviceProvider);
            return plugin;
        }
    }
}
