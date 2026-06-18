namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable payload for <c>POST /api/public/dataset-items</c>. Items are upserted on
/// <see cref="Id"/>. Property names are projected to camelCase by <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed record LangfuseCreateDatasetItemRequest
{
    /// <summary>Gets the dataset name the item belongs to.</summary>
    public required string DatasetName { get; init; }

    /// <summary>Gets the optional stable item id used for upsert.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the item input.</summary>
    public object? Input { get; init; }

    /// <summary>Gets the expected output.</summary>
    public object? ExpectedOutput { get; init; }

    /// <summary>Gets optional metadata.</summary>
    public object? Metadata { get; init; }

    /// <summary>Gets the optional source trace id.</summary>
    public string? SourceTraceId { get; init; }

    /// <summary>Gets the optional source observation id.</summary>
    public string? SourceObservationId { get; init; }

    /// <summary>Projects a public <see cref="LangfuseDatasetItem"/> to the wire payload.</summary>
    /// <param name="item">The dataset item to project.</param>
    /// <returns>The request payload.</returns>
    public static LangfuseCreateDatasetItemRequest From(LangfuseDatasetItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new LangfuseCreateDatasetItemRequest
        {
            DatasetName = item.DatasetName,
            Id = item.Id,
            Input = item.Input,
            ExpectedOutput = item.ExpectedOutput,
            Metadata = item.Metadata,
            SourceTraceId = item.SourceTraceId,
            SourceObservationId = item.SourceObservationId,
        };
    }
}
