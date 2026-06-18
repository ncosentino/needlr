namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseDatasetClient"/> backed by the shared <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed class LangfuseDatasetClient : ILangfuseDatasetClient
{
    private readonly LangfuseApiClient _apiClient;

    public LangfuseDatasetClient(LangfuseApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task EnsureDatasetAsync(string name, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var existing = await _apiClient
            .GetOrDefaultAsync<LangfuseDatasetRef>(
                $"api/public/v2/datasets/{Uri.EscapeDataString(name)}",
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return;
        }

        await _apiClient
            .PostAsync(
                "api/public/v2/datasets",
                new LangfuseCreateDatasetRequest { Name = name, Description = description },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpsertItemAsync(LangfuseDatasetItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.DatasetName);

        return _apiClient.PostAsync(
            "api/public/dataset-items",
            LangfuseCreateDatasetItemRequest.From(item),
            cancellationToken);
    }
}
