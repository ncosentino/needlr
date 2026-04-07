namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Internal write-side interface for storing diagnostics captured by middleware.
/// Separated from <see cref="IAgentDiagnosticsAccessor"/> (the public read-side) so that
/// the diagnostics plugin can resolve this without casting to a concrete type.
/// </summary>
internal interface IAgentDiagnosticsWriter
{
    /// <summary>
    /// Stores completed diagnostics into the current capture scope.
    /// Called by the diagnostics middleware after an agent run completes.
    /// </summary>
    void Set(IAgentRunDiagnostics diagnostics);
}
