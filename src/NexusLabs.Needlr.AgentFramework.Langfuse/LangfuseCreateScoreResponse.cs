namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Response returned by <c>POST /api/public/scores</c>.
/// </summary>
internal sealed record LangfuseCreateScoreResponse
{
    public string Id { get; init; } = string.Empty;
}
