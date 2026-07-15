using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>Wire projection returned by the Langfuse dataset-item list endpoint.</summary>
internal sealed record LangfuseDatasetItemDto
{
    public string Id { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public JsonElement? Input { get; init; }

    public JsonElement? ExpectedOutput { get; init; }

    public JsonElement? Metadata { get; init; }

    public string? SourceTraceId { get; init; }

    public string? SourceObservationId { get; init; }

    public string DatasetId { get; init; } = string.Empty;

    public string DatasetName { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
