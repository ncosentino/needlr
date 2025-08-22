using Microsoft.SemanticKernel;

namespace NexusLabs.Needlr.SemanticKernel;

public interface IKernelFactory
{
    Kernel CreateKernel(Action<KernelFactoryOptions>? configure = null);
}
