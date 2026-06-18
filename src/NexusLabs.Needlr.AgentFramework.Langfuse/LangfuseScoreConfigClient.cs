namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseScoreConfigClient"/> that ensures score configs via the shared
/// <see cref="LangfuseApiClient"/>. Existence is checked by paging
/// <c>GET /api/public/score-configs</c> and matching on name; a config is created only when absent.
/// </summary>
internal sealed class LangfuseScoreConfigClient : ILangfuseScoreConfigClient
{
    private const int PageSize = 100;

    private readonly LangfuseApiClient _apiClient;

    public LangfuseScoreConfigClient(LangfuseApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task EnsureScoreConfigAsync(LangfuseScoreConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name);

        if (await ConfigExistsAsync(config.Name, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await _apiClient
            .PostAsync("api/public/score-configs", LangfuseScoreConfigRequest.From(config), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ConfigExistsAsync(string name, CancellationToken cancellationToken)
    {
        var page = 1;
        while (true)
        {
            var response = await _apiClient
                .GetAsync<LangfuseScoreConfigsResponse>(
                    $"api/public/score-configs?page={page}&limit={PageSize}",
                    cancellationToken)
                .ConfigureAwait(false);

            if (response?.Data is not { Count: > 0 } items)
            {
                return false;
            }

            if (items.Any(c => string.Equals(c.Name, name, StringComparison.Ordinal)))
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
