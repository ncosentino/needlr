namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Reads, creates, and populates Langfuse datasets — the named collections of evaluation cases that
/// experiment runs are scored against.
/// </summary>
public interface ILangfuseDatasetClient
{
    /// <summary>
    /// Gets a value indicating whether Langfuse dataset operations are available. Mutating members
    /// are no-ops when <see langword="false"/>; read members fail explicitly rather than returning an
    /// empty dataset.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Lists one page of hosted datasets in provider order.</summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of datasets to request, from 1 through 100.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The validated dataset page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> or <paramref name="pageSize"/> is outside its supported range.
    /// </exception>
    /// <exception cref="InvalidOperationException">Langfuse is not configured.</exception>
    /// <exception cref="LangfuseException">The provider request or response is invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<LangfusePage<LangfuseDataset>> ListDatasetsAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists one page of active items from the latest or explicitly versioned hosted dataset.
    /// </summary>
    /// <param name="selection">The hosted dataset selection.</param>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of items to request, from 1 through 100.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The validated active-item page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The dataset name is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> or <paramref name="pageSize"/> is outside its supported range.
    /// </exception>
    /// <exception cref="InvalidOperationException">Langfuse is not configured.</exception>
    /// <exception cref="LangfuseException">The provider request or response is invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<LangfusePage<LangfuseDatasetItemSnapshot>> ListDatasetItemsAsync(
        LangfuseDatasetSelection selection,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads dataset metadata and fully materializes every active item before returning.
    /// </summary>
    /// <param name="selection">The hosted dataset selection.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The dataset metadata, selection, and complete ordered item snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The dataset name is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Langfuse is not configured.</exception>
    /// <exception cref="LangfuseException">The provider request or response is invalid or inconsistent.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<LangfuseDatasetSnapshot> GetDatasetAsync(
        LangfuseDatasetSelection selection,
        CancellationToken cancellationToken = default);

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
