namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates and populates Langfuse datasets — the named collections of eval cases that experiment
/// runs are scored against.
/// </summary>
public interface ILangfuseDatasetClient
{
    /// <summary>
    /// Gets a value indicating whether dataset operations are performed. <see langword="false"/>
    /// when Langfuse is not configured, in which case all members are no-ops.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Ensures a dataset with the given name exists, creating it only when absent. Safe to call on
    /// every run.
    /// </summary>
    /// <param name="name">The dataset name.</param>
    /// <param name="description">An optional description applied when the dataset is created.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the dataset exists in Langfuse.</returns>
    Task EnsureDatasetAsync(string name, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a dataset item. When <see cref="LangfuseDatasetItem.Id"/> is set, an existing item
    /// with that id is updated; otherwise a new item is created.
    /// </summary>
    /// <param name="item">The item to upsert.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the item has been persisted.</returns>
    Task UpsertItemAsync(LangfuseDatasetItem item, CancellationToken cancellationToken = default);
}
