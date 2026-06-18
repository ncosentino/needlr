namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Serializable payload for <c>POST /api/public/v2/datasets</c>.</summary>
internal sealed record LangfuseCreateDatasetRequest
{
    /// <summary>Gets the dataset name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; init; }
}
