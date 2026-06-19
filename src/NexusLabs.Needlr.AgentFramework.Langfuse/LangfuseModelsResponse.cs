namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Response shape for <c>GET /api/public/models</c>.</summary>
internal sealed record LangfuseModelsResponse
{
    /// <summary>Gets the models on the current page.</summary>
    public IReadOnlyList<LangfuseModelSummary> Data { get; init; } = [];

    /// <summary>Gets the pagination metadata.</summary>
    public LangfusePaginationMeta? Meta { get; init; }
}
