namespace NexusLabs.Needlr.AgentFramework.Context;

/// <summary>
/// Default implementation of <see cref="IAgentExecutionContext"/>. Immutable record that
/// carries user identity, orchestration ID, and an extensible property bag.
/// </summary>
/// <param name="UserId">The user identity for the current orchestration.</param>
/// <param name="OrchestrationId">Correlation ID for the current orchestration run.</param>
/// <param name="Properties">Extensible property bag for consumer-specific data.</param>
[DoNotAutoRegister]
public sealed record AgentExecutionContext(
    string UserId,
    string OrchestrationId,
    IReadOnlyDictionary<string, object>? Properties = null) : IAgentExecutionContext
{
    /// <inheritdoc />
    IReadOnlyDictionary<string, object> IAgentExecutionContext.Properties =>
        Properties ?? EmptyProperties.Instance;

    private static class EmptyProperties
    {
        internal static readonly IReadOnlyDictionary<string, object> Instance =
            new Dictionary<string, object>();
    }
}
