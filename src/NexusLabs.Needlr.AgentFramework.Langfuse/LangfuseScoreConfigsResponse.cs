namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Response shape for <c>GET /api/public/score-configs</c>.</summary>
internal sealed record LangfuseScoreConfigsResponse
{
    /// <summary>Gets the score configs on the current page.</summary>
    public IReadOnlyList<LangfuseScoreConfigSummary> Data { get; init; } = [];

    /// <summary>Gets the pagination metadata.</summary>
    public LangfusePaginationMeta? Meta { get; init; }
}
