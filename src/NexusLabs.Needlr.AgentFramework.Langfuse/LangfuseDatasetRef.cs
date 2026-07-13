namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Minimal projection of a dataset returned by <c>GET /api/public/v2/datasets/{name}</c>.
/// </summary>
internal sealed record LangfuseDatasetRef
{
    /// <summary>Gets the dataset name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the optional dataset description.</summary>
    public string? Description { get; init; }
}
