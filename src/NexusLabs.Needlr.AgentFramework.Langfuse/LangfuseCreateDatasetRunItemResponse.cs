namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Response from <c>POST /api/public/dataset-run-items</c>.
/// </summary>
internal sealed record LangfuseCreateDatasetRunItemResponse
{
    public required string Id { get; init; }

    public required string DatasetRunId { get; init; }

    public required string DatasetRunName { get; init; }

    public required string DatasetItemId { get; init; }

    public required string TraceId { get; init; }

    public required string? ObservationId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
