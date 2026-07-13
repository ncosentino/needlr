using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Minimal typed transport over the Langfuse public REST API (<c>/api/public/*</c>). Backs the
/// dataset, experiment, score-config, and comment features. Authenticates with HTTP Basic auth and
/// turns non-success responses into <see cref="LangfuseException"/>; per-feature mapping and
/// failure policy live in the recorders that compose it.
/// </summary>
/// <remarks>
/// This is deliberately separate from <see cref="LangfuseScoreApiClient"/>, which predates it and
/// owns the hot score-ingestion path. The underlying <see cref="HttpClient"/> is owned by the
/// caller and disposed with it.
/// </remarks>
internal sealed class LangfuseApiClient
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUrl;

    internal static JsonElement SerializeToElement(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value is JsonElement element
            ? element.Clone()
            : JsonSerializer.SerializeToElement(value, value.GetType(), SerializerOptions);
    }

    public LangfuseApiClient(HttpClient httpClient, Uri baseUrl, string authorizationHeaderValue)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationHeaderValue);

        _httpClient = httpClient;
        _baseUrl = baseUrl;

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
        {
            var space = authorizationHeaderValue.IndexOf(' ');
            _httpClient.DefaultRequestHeaders.Authorization = space > 0
                ? new System.Net.Http.Headers.AuthenticationHeaderValue(
                    authorizationHeaderValue[..space],
                    authorizationHeaderValue[(space + 1)..])
                : new System.Net.Http.Headers.AuthenticationHeaderValue(authorizationHeaderValue);
        }
    }

    /// <summary>Sends a POST and ignores the response body.</summary>
    /// <typeparam name="TRequest">The request payload type.</typeparam>
    /// <param name="relativePath">The API path relative to the base URL (no leading slash).</param>
    /// <param name="payload">The request payload, serialized as camelCase JSON.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="LangfuseException">The request failed or returned a non-success status.</exception>
    public async Task PostAsync<TRequest>(string relativePath, TRequest payload, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, relativePath, payload, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sends a POST and deserializes the response body.</summary>
    /// <typeparam name="TRequest">The request payload type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="relativePath">The API path relative to the base URL (no leading slash).</param>
    /// <param name="payload">The request payload, serialized as camelCase JSON.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized response, or <see langword="null"/> when the body is empty.</returns>
    /// <exception cref="LangfuseException">The request failed or returned a non-success status.</exception>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string relativePath, TRequest payload, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, relativePath, payload, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a GET and deserializes the response body.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="relativePath">The API path relative to the base URL (no leading slash).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized response, or <see langword="null"/> when the body is empty.</returns>
    /// <exception cref="LangfuseException">The request failed or returned a non-success status.</exception>
    public async Task<TResponse?> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendAsync<object?>(HttpMethod.Get, relativePath, payload: null, allowNotFound: false, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a GET and deserializes the body, returning <see langword="null"/> on <c>404 Not
    /// Found</c> instead of throwing. Used for existence checks (for example "does this dataset
    /// already exist?").
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="relativePath">The API path relative to the base URL (no leading slash).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized response, or <see langword="null"/> when not found or empty.</returns>
    /// <exception cref="LangfuseException">The request failed or returned a non-success status other than 404.</exception>
    public async Task<TResponse?> GetOrDefaultAsync<TResponse>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendAsync<object?>(HttpMethod.Get, relativePath, payload: null, allowNotFound: true, cancellationToken)
            .ConfigureAwait(false);

        return response.StatusCode is System.Net.HttpStatusCode.NotFound
            ? default
            : await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync<TRequest>(
        HttpMethod method,
        string relativePath,
        TRequest payload,
        CancellationToken cancellationToken)
        => await SendAsync(method, relativePath, payload, allowNotFound: false, cancellationToken).ConfigureAwait(false);

    private async Task<HttpResponseMessage> SendAsync<TRequest>(
        HttpMethod method,
        string relativePath,
        TRequest payload,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var uri = new Uri(_baseUrl, relativePath);
        using var request = new HttpRequestMessage(method, uri);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, mediaType: null, SerializerOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LangfuseException($"Langfuse request {method} '{uri}' failed.", ex);
        }

        if (response.IsSuccessStatusCode
            || (allowNotFound && response.StatusCode is System.Net.HttpStatusCode.NotFound))
        {
            return response;
        }

        var status = (int)response.StatusCode;
        var reason = response.ReasonPhrase;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.Dispose();

        throw new LangfuseException(
            $"Langfuse rejected {method} '{uri}' with status {status} ({reason}): {body}");
    }

    private static async Task<TResponse?> ReadAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is 0)
        {
            return default;
        }

        return await response.Content
            .ReadFromJsonAsync<TResponse>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
