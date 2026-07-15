namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Contains one hosted dataset and its complete ordered active-item selection.
/// </summary>
public sealed record LangfuseDatasetSnapshot
{
    /// <summary>Gets the dataset metadata.</summary>
    public required LangfuseDataset Dataset { get; init; }

    /// <summary>Gets the latest or timestamped selection used for item retrieval.</summary>
    public required LangfuseDatasetSelection Selection { get; init; }

    /// <summary>Gets every active item in provider order.</summary>
    public required IReadOnlyList<LangfuseDatasetItemSnapshot> Items { get; init; }
}
