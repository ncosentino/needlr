namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseDatasetClient"/> returned when Langfuse is not configured. Mutating
/// members are no-ops; read members fail explicitly so missing credentials cannot become an empty
/// passing experiment.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseDatasetClient : ILangfuseDatasetClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task<LangfusePage<LangfuseDataset>> ListDatasetsAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<LangfusePage<LangfuseDataset>>(
            new InvalidOperationException(
                "Langfuse datasets cannot be read because Langfuse is not configured."));
    }

    /// <inheritdoc />
    public Task<LangfusePage<LangfuseDatasetItemSnapshot>> ListDatasetItemsAsync(
        LangfuseDatasetSelection selection,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        selection.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<LangfusePage<LangfuseDatasetItemSnapshot>>(
            new InvalidOperationException(
                $"Langfuse dataset '{selection.Name}' cannot be read because Langfuse is not configured."));
    }

    /// <inheritdoc />
    public Task<LangfuseDatasetSnapshot> GetDatasetAsync(
        LangfuseDatasetSelection selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        selection.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<LangfuseDatasetSnapshot>(
            new InvalidOperationException(
                $"Langfuse dataset '{selection.Name}' cannot be loaded because Langfuse is not configured."));
    }

    /// <inheritdoc />
    public Task EnsureDatasetAsync(string name, string? description = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task UpsertItemAsync(LangfuseDatasetItem item, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
