namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Write-side interface for storing diagnostics captured by middleware. Separated from
/// <see cref="IAgentDiagnosticsAccessor"/> (the read-side) so that middleware can resolve
/// this from DI without casting to a concrete type.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton by <c>UsingAgentFramework()</c>. The default implementation
/// shares a backing store with <see cref="IAgentDiagnosticsAccessor"/>, so calling
/// <see cref="Set"/> makes the diagnostics immediately available via
/// <see cref="IAgentDiagnosticsAccessor.LastRunDiagnostics"/>.
/// </para>
/// <para>
/// Consumer middleware that captures agent diagnostics (e.g., a custom
/// <c>DiagnosticsAgentRunMiddleware</c>) should resolve this interface and call
/// <see cref="Set"/> after building an <see cref="IAgentRunDiagnostics"/> via
/// <see cref="AgentRunDiagnosticsBuilder"/>.
/// </para>
/// </remarks>
public interface IAgentDiagnosticsWriter
{
    /// <summary>
    /// Stores completed diagnostics into the current capture scope.
    /// Called by the diagnostics middleware after an agent run completes.
    /// </summary>
    void Set(IAgentRunDiagnostics diagnostics);
}
