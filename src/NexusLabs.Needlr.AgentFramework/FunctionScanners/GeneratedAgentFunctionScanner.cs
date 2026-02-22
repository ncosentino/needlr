namespace NexusLabs.Needlr.AgentFramework.FunctionScanners;

/// <summary>
/// Source-generation-friendly agent function scanner that uses compile-time generated function type lists.
/// </summary>
/// <remarks>
/// When using source generation, the generator emits a
/// <c>NexusLabs.Needlr.Generated.AgentFrameworkFunctions</c> class with
/// <c>AllFunctionTypes</c> containing all types with <see cref="AgentFunctionAttribute"/> methods.
///
/// For AOT/trimmed applications, use
/// <see cref="AgentFrameworkSyringeExtensions.AddAgentFunctionsFromGenerated"/> directly
/// (which requires no reflection for discovery) rather than this scanner.
/// </remarks>
public sealed class GeneratedAgentFunctionScanner : IAgentFrameworkFunctionScanner
{
    private readonly IReadOnlyList<Type> _functionTypes;

    public GeneratedAgentFunctionScanner(IReadOnlyList<Type> functionTypes)
    {
        ArgumentNullException.ThrowIfNull(functionTypes);
        _functionTypes = functionTypes;
    }

    public IReadOnlyList<Type> ScanForFunctionTypes() => _functionTypes;
}
