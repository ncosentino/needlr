namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Contains one validated page returned by a Langfuse list endpoint.
/// </summary>
/// <typeparam name="T">The listed resource type.</typeparam>
public sealed record LangfusePage<T>
{
    /// <summary>Gets the resources in provider order.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Gets the one-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Gets the requested and returned page size.</summary>
    public required int PageSize { get; init; }

    /// <summary>Gets the total number of matching resources.</summary>
    public required int TotalItems { get; init; }

    /// <summary>Gets the total number of pages.</summary>
    public required int TotalPages { get; init; }
}
