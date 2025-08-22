using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.SemanticKernel;

public static class SemanticKernelSyringeExtensions
{
    public static Syringe UsingKernelFactory(
        this Syringe syringe,
        Action<KernelFactoryOptions>? configure = null)
    {
        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.AddSingleton<IKernelFactory>(provider =>
            {
                KernelFactory kernelFactory = new(
                    provider,
                    _configure: configure);
                return kernelFactory;
            });
        });
    }
}
