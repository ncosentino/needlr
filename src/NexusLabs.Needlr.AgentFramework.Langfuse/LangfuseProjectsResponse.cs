namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Response shape for <c>GET /api/public/projects</c>. The Basic-auth API key resolves to a single
/// project, which the comment feature needs because <c>POST /api/public/comments</c> requires an
/// explicit project id.
/// </summary>
internal sealed record LangfuseProjectsResponse
{
    /// <summary>Gets the projects accessible to the authenticated API key.</summary>
    public IReadOnlyList<LangfuseProjectRef> Data { get; init; } = [];
}
