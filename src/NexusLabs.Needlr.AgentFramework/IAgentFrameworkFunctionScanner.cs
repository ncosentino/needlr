namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Discovers types containing methods decorated with <see cref="AgentFunctionAttribute"/>.
/// The source generator emits <c>GeneratedAgentFunctionScanner</c> at compile time;
/// in reflection mode, a runtime scanner is used instead.
/// </summary>
/// <remarks>
/// <para>
/// The discovered types are passed to <see cref="IAgentFactory"/> so that agent
/// creation can resolve tool functions from them. Each type may contain multiple
/// <c>[AgentFunction]</c> methods, and each method becomes an <c>AIFunction</c>
/// available to agents that reference the type's function group.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Typically resolved from DI — not called directly by application code.
/// var scanner = serviceProvider.GetRequiredService&lt;IAgentFrameworkFunctionScanner&gt;();
/// var functionTypes = scanner.ScanForFunctionTypes();
/// Console.WriteLine($"Found {functionTypes.Count} types with [AgentFunction] methods");
/// </code>
/// </example>
public interface IAgentFrameworkFunctionScanner
{
    /// <summary>
    /// Returns all types that contain at least one method decorated with
    /// <see cref="AgentFunctionAttribute"/>. The returned list is used by the
    /// agent factory to build the tool set for each agent.
    /// </summary>
    IReadOnlyList<Type> ScanForFunctionTypes();
}
