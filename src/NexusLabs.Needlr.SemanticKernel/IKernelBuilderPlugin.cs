namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Defines a plugin that configures the Semantic Kernel builder before the kernel is built.
/// Implement this interface to add AI services, configure logging, or modify the kernel builder.
/// </summary>
[DoNotAutoRegister]
[DoNotInject]
public interface IKernelBuilderPlugin
{
    void Configure(KernelBuilderPluginOptions options);
}
