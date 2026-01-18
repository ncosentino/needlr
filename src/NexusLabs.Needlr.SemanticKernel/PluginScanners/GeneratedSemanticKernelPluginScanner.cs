namespace NexusLabs.Needlr.SemanticKernel.PluginScanners;

/// <summary>
/// Source-generation-friendly SemanticKernel plugin scanner that uses compile-time generated plugin type lists.
/// </summary>
/// <remarks>
/// When using source generation, the generator emits a 
/// <c>NexusLabs.Needlr.Generated.SemanticKernelPlugins</c> class containing:
/// - <c>StaticPluginTypes</c>: Types with static [KernelFunction] methods
/// - <c>InstancePluginTypes</c>: Types with instance [KernelFunction] methods
/// - <c>AllPluginTypes</c>: Combined list of all plugin types
/// 
/// This scanner provides a reflection-free way to access these generated lists.
/// For AOT/trimmed applications, use this scanner instead of 
/// <see cref="AssemblySemanticKernelPluginScanner"/> or 
/// <see cref="ServiceProviderSemanticKernelPluginScanner"/> which use reflection.
/// 
/// Usage:
/// <code>
/// // The generated types are available in NexusLabs.Needlr.Generated namespace
/// var pluginTypes = NexusLabs.Needlr.Generated.SemanticKernelPlugins.AllPluginTypes;
/// 
/// // Or use this scanner wrapper
/// var scanner = new GeneratedSemanticKernelPluginScanner();
/// var types = scanner.ScanForPluginTypes();
/// </code>
/// </remarks>
public sealed class GeneratedSemanticKernelPluginScanner : ISemanticKernelPluginScanner
{
    private readonly IReadOnlyList<Type> _pluginTypes;

    /// <summary>
    /// Creates a new instance using the specified plugin types.
    /// </summary>
    /// <param name="pluginTypes">The pre-discovered plugin types (typically from generated code).</param>
    public GeneratedSemanticKernelPluginScanner(IReadOnlyList<Type> pluginTypes)
    {
        ArgumentNullException.ThrowIfNull(pluginTypes);
        _pluginTypes = pluginTypes;
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> ScanForPluginTypes() => _pluginTypes;
}
