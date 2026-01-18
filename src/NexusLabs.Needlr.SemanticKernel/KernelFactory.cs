using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel;

internal sealed class KernelFactory(
    IServiceProvider _serviceProvider,
    IPluginFactory _pluginFactory,
    Action<KernelFactoryOptions>? _configure) :
    IKernelFactory
{
    private readonly Lazy<IReadOnlyList<IKernelBuilderPlugin>> _lazyKernelBuilderPlugins = new(() =>
    {
        return _pluginFactory
            .CreatePluginsFromAssemblies<IKernelBuilderPlugin>(_serviceProvider
                .GetRequiredService<IReadOnlyList<Assembly>>())
            .ToArray();
    });

    public Kernel CreateKernel(Action<KernelFactoryOptions>? configure = null)
    {
        var scope = _serviceProvider.CreateScope();
        var kernelBuilder = Kernel.CreateBuilder();

        scope.ServiceProvider.CopyRegistrationsToServiceCollection(kernelBuilder.Services);

        KernelBuilderPluginOptions pluginOptions = new(kernelBuilder);
        foreach (var plugin in _lazyKernelBuilderPlugins.Value)
        {
            plugin.Configure(pluginOptions);
        }

        KernelFactoryOptions kernelFactoryOptions = new(
            scope.ServiceProvider,
            kernelBuilder);
        _configure?.Invoke(kernelFactoryOptions);
        configure?.Invoke(kernelFactoryOptions);

        kernelBuilder.Services.AddSingleton(_ => new ScopeDisposer(scope));
        var kernel = kernelBuilder.Build();
        _ = kernel.Services.GetRequiredService<ScopeDisposer>();

        return kernel;
    }

    private sealed class ScopeDisposer(IServiceScope scope) : IDisposable
    {
        public void Dispose() => scope.Dispose();
    }
}
