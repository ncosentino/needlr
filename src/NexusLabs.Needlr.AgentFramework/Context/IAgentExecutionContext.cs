namespace NexusLabs.Needlr.AgentFramework.Context;

/// <summary>
/// Per-orchestration execution context established by trusted non-LLM code (API auth middleware,
/// workflow engine, scenario harness) before invoking an agent. Tools read this via
/// <see cref="IAgentExecutionContextAccessor"/> to obtain the user identity and orchestration
/// correlation ID.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should be immutable. Use <c>with</c> expressions (if a record) to create a
/// derived context for sub-agents or nested scopes.
/// </para>
/// <para>
/// This interface intentionally does not include workspace, provider, or other concerns.
/// Consumers that need a workspace can create their own implementation that carries one,
/// or store it in <see cref="Properties"/>.
/// </para>
/// </remarks>
public interface IAgentExecutionContext
{
    /// <summary>Gets the user identity for the current orchestration.</summary>
    /// <remarks>
    /// Set by trusted callers only — never by the LLM or by tool parameters.
    /// </remarks>
    string UserId { get; }

    /// <summary>Gets the correlation ID for the current orchestration run.</summary>
    string OrchestrationId { get; }

    /// <summary>
    /// Gets an extensible property bag for consumer-specific data (workspace, tenant, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Framework code never reads from this bag. It exists so consumers can attach domain-specific
    /// data to the context without creating a custom <see cref="IAgentExecutionContext"/> implementation.
    /// </para>
    /// <para>
    /// Implementations should return a read-only dictionary. Mutability (if needed) should be
    /// handled by creating a new context with updated properties.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<string, object> Properties { get; }
}
