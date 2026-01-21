namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Defines a scanner that discovers Semantic Kernel plugin types for registration.
/// Implement this interface to customize how plugins are discovered.
/// </summary>
public interface ISemanticKernelPluginScanner
{
    IReadOnlyList<Type> ScanForPluginTypes();
}
