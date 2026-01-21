using Microsoft.SemanticKernel;

namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Options provided to kernel factory configuration callbacks.
/// </summary>
/// <param name="ServiceProvider">The service provider for resolving dependencies.</param>
/// <param name="KernelBuilder">The kernel builder being configured.</param>
public sealed record KernelFactoryOptions(
    IServiceProvider ServiceProvider,
    IKernelBuilder KernelBuilder);