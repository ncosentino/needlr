using Microsoft.SemanticKernel;

namespace NexusLabs.Needlr.SemanticKernel;

public sealed record KernelFactoryOptions(
    IServiceProvider ServiceProvider,
    IKernelBuilder KernelBuilder);