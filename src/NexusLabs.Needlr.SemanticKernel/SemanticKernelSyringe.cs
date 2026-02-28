using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Fluent builder for configuring Semantic Kernel with Needlr plugin discovery.
/// </summary>
/// <remarks>
/// This class uses reflection to discover methods with <see cref="KernelFunctionAttribute"/>.
/// For AOT/trimmed applications, consider registering kernel functions explicitly.
/// </remarks>
/// <example>
/// <code>
/// // Obtained from SyringeExtensionsForSemanticKernel.UsingSemanticKernel()
/// SemanticKernelSyringe syringe = app.Services.UsingSemanticKernel();
///
/// // Register plugins and build the kernel factory
/// IKernelFactory kernelFactory = syringe
///     .AddSemanticKernelPluginsFromGenerated(GeneratedSemanticKernelPlugins.AllPluginTypes)
///     .BuildKernelFactory();
///
/// // Create a Kernel instance from the factory
/// Kernel kernel = kernelFactory.CreateKernel();
/// </code>
/// </example>
[DoNotAutoRegister]
[RequiresUnreferencedCode("SemanticKernel plugin setup uses reflection to discover [KernelFunction] methods.")]
[RequiresDynamicCode("SemanticKernel plugin setup uses reflection APIs that require dynamic code generation.")]
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
