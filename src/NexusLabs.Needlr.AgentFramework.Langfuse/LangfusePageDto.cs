namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Shared wire projection for paginated Langfuse list responses.</summary>
internal sealed record LangfusePageDto<T>
{
    public IReadOnlyList<T>? Data { get; init; }

    public LangfusePaginationMeta? Meta { get; init; }
}
