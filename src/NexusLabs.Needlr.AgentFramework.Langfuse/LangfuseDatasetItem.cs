namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// A single Langfuse dataset item — one eval case. Upserted via
/// <see cref="ILangfuseDatasetClient.UpsertItemAsync"/> and later referenced by an experiment run.
/// </summary>
/// <remarks>
/// <see cref="Input"/>, <see cref="ExpectedOutput"/>, and <see cref="Metadata"/> are serialized to
/// JSON, so a string, anonymous object, dictionary, or POCO are all valid.
/// </remarks>
public sealed record LangfuseDatasetItem
{
    /// <summary>Gets the name of the dataset this item belongs to.</summary>
    public required string DatasetName { get; init; }

    /// <summary>
    /// Gets the stable item id used for upsert. Reusing the same id updates the item rather than
    /// creating a duplicate. Recommended so re-running an eval suite does not duplicate items.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>Gets the item input (the eval prompt or request).</summary>
    public object? Input { get; init; }

    /// <summary>Gets the expected output used as the reference for scoring.</summary>
    public object? ExpectedOutput { get; init; }

    /// <summary>Gets optional metadata stored alongside the item.</summary>
    public object? Metadata { get; init; }

    /// <summary>Gets the optional id of a production trace this item was curated from.</summary>
    public string? SourceTraceId { get; init; }

    /// <summary>Gets the optional id of a production observation this item was curated from.</summary>
    public string? SourceObservationId { get; init; }
}
