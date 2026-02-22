namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Defines a scanner that discovers types containing
/// <see cref="AgentFunctionAttribute"/>-decorated methods.
/// </summary>
public interface IAgentFrameworkFunctionScanner
{
    /// <summary>
    /// Scans for types that contain at least one method decorated with
    /// <see cref="AgentFunctionAttribute"/>.
    /// </summary>
    IReadOnlyList<Type> ScanForFunctionTypes();
}
