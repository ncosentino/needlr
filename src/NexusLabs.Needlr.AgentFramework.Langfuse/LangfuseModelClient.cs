namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseModelClient"/> backed by the shared <see cref="LangfuseApiClient"/>.
/// Existence is checked by paging <c>GET /api/public/models</c> and matching on model name; a
/// definition is created only when absent.
/// </summary>
internal sealed class LangfuseModelClient : ILangfuseModelClient
{
    private const int PageSize = 100;

    private readonly LangfuseApiClient _apiClient;

    public LangfuseModelClient(LangfuseApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task EnsureModelPriceAsync(LangfuseModelPrice price, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(price.ModelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(price.MatchPattern);

        if (await ModelExistsAsync(price.ModelName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await _apiClient
            .PostAsync("api/public/models", LangfuseCreateModelRequest.From(price), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ModelExistsAsync(string modelName, CancellationToken cancellationToken)
    {
        var page = 1;
        while (true)
        {
            var response = await _apiClient
                .GetAsync<LangfuseModelsResponse>(
                    $"api/public/models?page={page}&limit={PageSize}",
                    cancellationToken)
                .ConfigureAwait(false);

            if (response?.Data is not { Count: > 0 } items)
            {
                return false;
            }

            if (items.Any(m => string.Equals(m.ModelName, modelName, StringComparison.Ordinal)))
            {
                return true;
            }

            if (response.Meta is not { } meta || page >= meta.TotalPages)
            {
                return false;
            }

            page++;
        }
    }
}
