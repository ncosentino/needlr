namespace NexusLabs.Needlr.SemanticKernel;

[DoNotAutoRegister]
[DoNotInject]
public interface IKernelBuilderPlugin
{
    void Configure(KernelBuilderPluginOptions options);
}
