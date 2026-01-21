using Microsoft.SemanticKernel;

namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Factory interface for creating configured <see cref="Kernel"/> instances with Needlr's auto-discovery capabilities.
/// </summary>
public interface IKernelFactory
{
    Kernel CreateKernel(Action<KernelFactoryOptions>? configure = null);
}
