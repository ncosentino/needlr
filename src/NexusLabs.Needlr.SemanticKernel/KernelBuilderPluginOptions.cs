using Microsoft.SemanticKernel;

namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Options provided to <see cref="IKernelBuilderPlugin"/> implementations during configuration.
/// </summary>
/// <param name="KernelBuilder">The kernel builder being configured.</param>
public sealed record KernelBuilderPluginOptions(
    IKernelBuilder KernelBuilder);
