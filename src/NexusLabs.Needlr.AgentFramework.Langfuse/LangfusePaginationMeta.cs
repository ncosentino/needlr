namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Pagination metadata returned by Langfuse list endpoints.</summary>
internal sealed record LangfusePaginationMeta
{
    /// <summary>Gets the total number of pages available for the query.</summary>
    public int TotalPages { get; init; }
}
