namespace NexusLabs.Needlr.SemanticKernel;

public interface ISemanticKernelPluginScanner
{
    IReadOnlyList<Type> ScanForPluginTypes();
}
