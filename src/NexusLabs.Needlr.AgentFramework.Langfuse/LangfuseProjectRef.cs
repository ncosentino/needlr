namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// A single project entry returned by <c>GET /api/public/projects</c>. Only the id is consumed.
/// </summary>
internal sealed record LangfuseProjectRef
{
    /// <summary>Gets the project id.</summary>
    public string Id { get; init; } = string.Empty;
}
