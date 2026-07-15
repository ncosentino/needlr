namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Pagination metadata returned by Langfuse list endpoints.</summary>
internal sealed record LangfusePaginationMeta
{
    /// <summary>Gets the current one-based page number.</summary>
    public int Page { get; init; }

    /// <summary>Gets the number of resources requested per page.</summary>
    public int Limit { get; init; }

    /// <summary>Gets the total number of matching resources.</summary>
    public int TotalItems { get; init; }

    /// <summary>Gets the total number of pages available for the query.</summary>
    public int TotalPages { get; init; }
}
