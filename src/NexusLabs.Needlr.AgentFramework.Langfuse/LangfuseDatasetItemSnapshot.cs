using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Captures one active hosted Langfuse dataset item at the selected dataset version.
/// </summary>
public sealed record LangfuseDatasetItemSnapshot
{
    /// <summary>Gets the stable project-wide dataset item identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the stable identifier of the containing dataset.</summary>
    public required string DatasetId { get; init; }

    /// <summary>Gets the name of the containing dataset.</summary>
    public required string DatasetName { get; init; }

    /// <summary>Gets the item input.</summary>
    public JsonElement? Input { get; init; }

    /// <summary>Gets the optional expected output.</summary>
    public JsonElement? ExpectedOutput { get; init; }

    /// <summary>Gets optional structured item metadata.</summary>
    public JsonElement? Metadata { get; init; }

    /// <summary>Gets the optional production trace from which the item was curated.</summary>
    public string? SourceTraceId { get; init; }

    /// <summary>Gets the optional production observation from which the item was curated.</summary>
    public string? SourceObservationId { get; init; }

    /// <summary>Gets the item creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the item update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
