namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseModelClient"/> backed by the shared <see cref="LangfuseApiClient"/>.
/// Creation is coordinated through a resource lock, and every same-name model is compared against
/// the complete requested definition before creation.
/// </summary>
internal sealed class LangfuseModelClient : ILangfuseModelClient
{
    private const int PageSize = 100;

    private readonly LangfuseApiClient _apiClient;
    private readonly ILangfuseResourceLockProvider _resourceLockProvider;
    private readonly string _resourceLockScope;

    public LangfuseModelClient(
        LangfuseApiClient apiClient,
        ILangfuseResourceLockProvider resourceLockProvider,
        string resourceLockScope)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(resourceLockProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceLockScope);

        _apiClient = apiClient;
        _resourceLockProvider = resourceLockProvider;
        _resourceLockScope = resourceLockScope;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task EnsureModelPriceAsync(LangfuseModelPrice price, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(price.ModelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(price.MatchPattern);

        var request = LangfuseCreateModelRequest.From(price);
        await _apiClient
            .EnsureResourceAsync(
                _resourceLockProvider,
                LangfuseResourceLockKey.Create(
                    _resourceLockScope,
                    "model",
                    price.ModelName),
                token => ModelMatchesAsync(request, token),
                token => _apiClient.PostAsync(
                    "api/public/models",
                    request,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ModelMatchesAsync(
        LangfuseCreateModelRequest expected,
        CancellationToken cancellationToken)
    {
        var matches = new List<LangfuseModelSummary>();
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
                break;
            }

            matches.AddRange(items.Where(model =>
                string.Equals(model.ModelName, expected.ModelName, StringComparison.Ordinal)));

            if (response.Meta is not { } meta || page >= meta.TotalPages)
            {
                break;
            }

            page++;
        }

        if (matches.Count == 0)
        {
            return false;
        }

        if (matches.Count != 1 || !Matches(matches[0], expected))
        {
            throw new LangfuseException(
                $"Langfuse model '{expected.ModelName}' already exists with a different or ambiguous definition.");
        }

        return true;
    }

    private static bool Matches(
        LangfuseModelSummary actual,
        LangfuseCreateModelRequest expected) =>
        string.Equals(actual.MatchPattern, expected.MatchPattern, StringComparison.Ordinal)
        && string.Equals(actual.Unit, expected.Unit, StringComparison.Ordinal)
        && actual.InputPrice == expected.InputPrice
        && actual.OutputPrice == expected.OutputPrice
        && actual.TotalPrice == expected.TotalPrice;
}
