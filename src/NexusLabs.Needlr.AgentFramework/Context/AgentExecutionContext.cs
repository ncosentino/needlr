namespace NexusLabs.Needlr.AgentFramework.Context;

/// <summary>
/// Default implementation of <see cref="IAgentExecutionContext"/>. Immutable record that
/// carries user identity, orchestration ID, an optional workspace, and an extensible
/// property bag.
/// </summary>
/// <param name="UserId">The user identity for the current orchestration.</param>
/// <param name="OrchestrationId">Correlation ID for the current orchestration run.</param>
/// <param name="Workspace">Optional workspace for agent file operations. Stored in
/// <see cref="IAgentExecutionContext.Properties"/> under the <see cref="Workspace.IWorkspace"/>
/// type key so it is accessible via <see cref="AgentExecutionContextExtensions.GetWorkspace"/>.</param>
/// <param name="Properties">Extensible property bag for consumer-specific data.</param>
[DoNotAutoRegister]
public sealed record AgentExecutionContext(
    string UserId,
    string OrchestrationId,
    IReadOnlyDictionary<string, object>? Properties = null,
    Workspace.IWorkspace? Workspace = null) : IAgentExecutionContext
{
    /// <inheritdoc />
    IReadOnlyDictionary<string, object> IAgentExecutionContext.Properties =>
        BuildProperties();

    private IReadOnlyDictionary<string, object> BuildProperties()
    {
        if (Workspace is null)
            return Properties ?? EmptyProperties.Instance;

        // Merge the workspace into the property bag so GetWorkspace() works
        // regardless of whether the consumer uses this default implementation
        // or a custom one.
        var merged = Properties is not null
            ? new Dictionary<string, object>(Properties)
            : new Dictionary<string, object>();

        merged[typeof(Workspace.IWorkspace).FullName!] = Workspace;
        return merged;
    }

    private static class EmptyProperties
    {
        internal static readonly IReadOnlyDictionary<string, object> Instance =
            new Dictionary<string, object>();
    }
}
