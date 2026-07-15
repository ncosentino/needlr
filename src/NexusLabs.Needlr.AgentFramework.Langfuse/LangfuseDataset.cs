using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes one hosted Langfuse dataset.
/// </summary>
public sealed record LangfuseDataset
{
    /// <summary>Gets the stable Langfuse dataset identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the project-unique dataset name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the optional dataset description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets optional structured dataset metadata.</summary>
    public JsonElement? Metadata { get; init; }

    /// <summary>Gets the optional JSON Schema applied to dataset item inputs.</summary>
    public JsonElement? InputSchema { get; init; }

    /// <summary>Gets the optional JSON Schema applied to expected outputs.</summary>
    public JsonElement? ExpectedOutputSchema { get; init; }

    /// <summary>Gets the dataset creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the dataset update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
