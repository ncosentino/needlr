namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseScoreConfigClient"/> that ensures score configs via the shared
/// <see cref="LangfuseApiClient"/>. Creation is coordinated through a resource lock, and every
/// same-name config is compared against the complete requested schema before creation.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseScoreConfigClient : ILangfuseScoreConfigClient
{
    private const int PageSize = 100;

    private readonly LangfuseApiClient _apiClient;
    private readonly ILangfuseResourceLockProvider _resourceLockProvider;
    private readonly string _resourceLockScope;

    public LangfuseScoreConfigClient(
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
    public async Task EnsureScoreConfigAsync(LangfuseScoreConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.Name);

        var request = LangfuseScoreConfigRequest.From(config);
        await _apiClient
            .EnsureResourceAsync(
                _resourceLockProvider,
                LangfuseResourceLockKey.Create(
                    _resourceLockScope,
                    "score-config",
                    config.Name),
                token => ConfigMatchesAsync(request, token),
                token => _apiClient.PostAsync(
                    "api/public/score-configs",
                    request,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> ConfigMatchesAsync(
        LangfuseScoreConfigRequest expected,
        CancellationToken cancellationToken)
    {
        var configs = await _apiClient
            .GetAllPagesAsync<LangfuseScoreConfigSummary>(
                page => $"api/public/score-configs?page={page}&limit={PageSize}",
                cancellationToken)
            .ConfigureAwait(false);
        var matches = configs
            .Where(config =>
                string.Equals(config.Name, expected.Name, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 0)
        {
            return false;
        }

        if (matches.Length != 1 || !Matches(matches[0], expected))
        {
            throw new LangfuseException(
                $"Langfuse score config '{expected.Name}' already exists with a different or ambiguous schema.");
        }

        return true;
    }

    private static bool Matches(
        LangfuseScoreConfigSummary actual,
        LangfuseScoreConfigRequest expected) =>
        string.Equals(actual.DataType, expected.DataType, StringComparison.Ordinal)
        && actual.MinValue == expected.MinValue
        && actual.MaxValue == expected.MaxValue
        && string.Equals(actual.Description, expected.Description, StringComparison.Ordinal)
        && CategoriesMatch(actual.Categories, expected.Categories);

    private static bool CategoriesMatch(
        IReadOnlyList<LangfuseScoreConfigCategory>? actual,
        IReadOnlyList<LangfuseScoreConfigCategory>? expected)
    {
        var actualItems = actual ?? [];
        var expectedItems = expected ?? [];
        return actualItems.Count == expectedItems.Count
            && actualItems
                .Zip(expectedItems)
                .All(pair =>
                    string.Equals(pair.First.Label, pair.Second.Label, StringComparison.Ordinal)
                    && pair.First.Value == pair.Second.Value);
    }
}
