using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable payload for <c>POST /api/public/dataset-run-items</c>, the join that links a trace
/// to a dataset item within a named run. Property names are projected to camelCase by
/// <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed record LangfuseCreateDatasetRunItemRequest
{
    /// <summary>Gets the run name. Langfuse creates the run on first use.</summary>
    public required string RunName { get; init; }

    /// <summary>Gets the optional run description submitted with the item link.</summary>
    public string? RunDescription { get; init; }

    /// <summary>Gets optional structured metadata submitted for the dataset run.</summary>
    public JsonElement? Metadata { get; init; }

    /// <summary>Gets the id of the dataset item being evaluated.</summary>
    public required string DatasetItemId { get; init; }

    /// <summary>Gets the id of the trace produced while evaluating the item.</summary>
    public required string TraceId { get; init; }

    /// <summary>Gets the optional id of a specific observation within the trace.</summary>
    public string? ObservationId { get; init; }
}
