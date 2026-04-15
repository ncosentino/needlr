using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Acquires and caches short-lived Copilot API tokens by exchanging a GitHub OAuth token
/// (from <see cref="IGitHubOAuthTokenProvider"/>) via the internal GitHub API endpoint.
/// Thread-safe: concurrent callers share a single refresh via <see cref="SemaphoreSlim"/>.
/// </summary>
internal sealed class CopilotTokenProvider : ICopilotTokenProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CopilotChatClientOptions _options;
    private readonly IGitHubOAuthTokenProvider _oauthProvider;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public CopilotTokenProvider(CopilotChatClientOptions options, HttpClient? httpClient = null)
        : this(new GitHubOAuthTokenProvider(options), options, httpClient)
    {
    }

    public CopilotTokenProvider(
        IGitHubOAuthTokenProvider oauthProvider,
        CopilotChatClientOptions options,
        HttpClient? httpClient = null)
    {
        _oauthProvider = oauthProvider ?? throw new ArgumentNullException(nameof(oauthProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null &&
            DateTimeOffset.UtcNow.AddSeconds(_options.TokenRefreshBufferSeconds) < _expiresAt)
        {
            return _cachedToken;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock
            if (_cachedToken is not null &&
                DateTimeOffset.UtcNow.AddSeconds(_options.TokenRefreshBufferSeconds) < _expiresAt)
            {
                return _cachedToken;
            }

            var oauthToken = _oauthProvider.GetOAuthToken();
            var response = await ExchangeTokenAsync(oauthToken, cancellationToken).ConfigureAwait(false);

            _cachedToken = response.Token;
            _expiresAt = DateTimeOffset.FromUnixTimeSeconds(response.ExpiresAt);

            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<CopilotTokenResponse> ExchangeTokenAsync(
        string oauthToken, CancellationToken cancellationToken)
    {
        var url = $"{_options.GitHubApiBaseUrl.TrimEnd('/')}/copilot_internal/v2/token";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"token {oauthToken}");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", _options.IntegrationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Copilot token exchange failed ({response.StatusCode}): {body}");
        }

        var tokenResponse = await response.Content
            .ReadFromJsonAsync(CopilotJsonContext.Default.CopilotTokenResponse, cancellationToken)
            .ConfigureAwait(false);

        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.Token))
        {
            throw new InvalidOperationException("Copilot token exchange returned an empty token.");
        }

        return tokenResponse;
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

internal sealed record CopilotTokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = "";

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; init; }
}
