using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Minimal typed transport over the Langfuse public REST API (<c>/api/public/*</c>). Backs the
/// dataset, experiment, score-config, and comment features. Authenticates with HTTP Basic auth and
/// applies bounded retries only to provider-idempotent operations, and turns terminal failures
/// into <see cref="LangfuseException"/>. Per-feature mapping and failure policy live in the
/// recorders that compose it.
/// </summary>
/// <remarks>
/// The underlying <see cref="HttpClient"/> is owned by the caller and disposed with it.
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
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialRetryDelay;
    private readonly TimeSpan _maxRetryDelay;
    private readonly LangfusePublicationHealth _health;

    internal static JsonElement SerializeToElement(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value is JsonElement element
            ? element.Clone()
            : JsonSerializer.SerializeToElement(value, value.GetType(), SerializerOptions);
    }

    public LangfuseApiClient(
        HttpClient httpClient,
        Uri baseUrl,
        string authorizationHeaderValue,
        LangfuseHttpOptions? httpOptions = null,
        TimeProvider? timeProvider = null,
        LangfusePublicationHealth? health = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationHeaderValue);
        httpOptions ??= new LangfuseHttpOptions();
        httpOptions.Validate();

        _httpClient = httpClient;
        _baseUrl = baseUrl;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _requestTimeout = httpOptions.RequestTimeout;
        _maxAttempts = httpOptions.MaxAttempts;
        _initialRetryDelay = httpOptions.InitialRetryDelay;
        _maxRetryDelay = httpOptions.MaxRetryDelay;
        _health = health ?? new LangfusePublicationHealth(isEnabled: true);

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
        using var response = await SendAsync(
            HttpMethod.Post,
            relativePath,
            payload,
            LangfuseHttpRetryMode.None,
            allowNotFound: false,
            cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task PostIdempotentAsync<TRequest>(
        string relativePath,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            relativePath,
            payload,
            LangfuseHttpRetryMode.Idempotent,
            allowNotFound: false,
            cancellationToken)
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
        using var response = await SendAsync(
            HttpMethod.Post,
            relativePath,
            payload,
            LangfuseHttpRetryMode.None,
            allowNotFound: false,
            cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<TResponse?> PostIdempotentAsync<TRequest, TResponse>(
        string relativePath,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            relativePath,
            payload,
            LangfuseHttpRetryMode.Idempotent,
            allowNotFound: false,
            cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    internal async Task EnsureResourceAsync(
        ILangfuseResourceLockProvider lockProvider,
        string lockKey,
        Func<CancellationToken, Task<bool>> resourceMatchesAsync,
        Func<CancellationToken, Task> createAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lockProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentNullException.ThrowIfNull(resourceMatchesAsync);
        ArgumentNullException.ThrowIfNull(createAsync);

        await using var resourceLock = await lockProvider
            .AcquireAsync(lockKey, cancellationToken)
            .ConfigureAwait(false);

        if (await resourceMatchesAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await createAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (LangfuseHttpException ex)
                when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                if (await resourceMatchesAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                throw;
            }
            catch (LangfuseHttpException ex) when (ex.IsTransient)
            {
                if (await resourceMatchesAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                if (attempt >= _maxAttempts)
                {
                    throw;
                }

                _health.RecordRetry(GetRetryReason(ex));
                await DelayBeforeRetryAsync(ex, attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Sends a GET and deserializes the response body.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="relativePath">The API path relative to the base URL (no leading slash).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized response, or <see langword="null"/> when the body is empty.</returns>
    /// <exception cref="LangfuseException">The request failed or returned a non-success status.</exception>
    public async Task<TResponse?> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendAsync<object?>(
            HttpMethod.Get,
            relativePath,
            payload: null,
            LangfuseHttpRetryMode.Idempotent,
            allowNotFound: false,
            cancellationToken)
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
        using var response = await SendAsync<object?>(
            HttpMethod.Get,
            relativePath,
            payload: null,
            LangfuseHttpRetryMode.Idempotent,
            allowNotFound: true,
            cancellationToken)
            .ConfigureAwait(false);

        return response.StatusCode is System.Net.HttpStatusCode.NotFound
            ? default
            : await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync<TRequest>(
        HttpMethod method,
        string relativePath,
        TRequest payload,
        LangfuseHttpRetryMode retryMode,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var uri = new Uri(_baseUrl, relativePath);
        var serializedPayload = payload is null
            ? null
            : JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);

        for (var attempt = 1; ; attempt++)
        {
            using var request = CreateRequest(method, uri, serializedPayload);
            using var requestTimeout = new CancellationTokenSource(_requestTimeout, _timeProvider);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                requestTimeout.Token);

            HttpResponseMessage? response = null;
            LangfuseHttpException? failure = null;
            try
            {
                response = await _httpClient
                    .SendAsync(request, linkedCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(ex.Message, ex, cancellationToken);
            }
            catch (OperationCanceledException ex) when (requestTimeout.IsCancellationRequested)
            {
                failure = new LangfuseHttpException(
                    $"Langfuse request {method} '{uri}' timed out after {_requestTimeout}.",
                    statusCode: null,
                    retryAfter: null,
                    isTransient: true,
                    isTimeout: true,
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                failure = new LangfuseHttpException(
                    $"Langfuse request {method} '{uri}' timed out.",
                    statusCode: null,
                    retryAfter: null,
                    isTransient: true,
                    isTimeout: true,
                    ex);
            }
            catch (HttpRequestException ex)
            {
                failure = new LangfuseHttpException(
                    $"Langfuse request {method} '{uri}' failed.",
                    statusCode: ex.StatusCode,
                    retryAfter: null,
                    isTransient: true,
                    isTimeout: false,
                    ex);
            }

            if (response is not null)
            {
                if (response.IsSuccessStatusCode
                    || (allowNotFound && response.StatusCode is HttpStatusCode.NotFound))
                {
                    return response;
                }

                var statusCode = response.StatusCode;
                var reasonPhrase = response.ReasonPhrase;
                var retryAfter = GetRetryAfter(response);
                string body;
                try
                {
                    body = await ReadFailureBodyAsync(response, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    response.Dispose();
                }

                failure = new LangfuseHttpException(
                    $"Langfuse rejected {method} '{uri}' with status {(int)statusCode} " +
                    $"({reasonPhrase}): {body}",
                    statusCode,
                    retryAfter,
                    IsTransient(statusCode),
                    isTimeout: false);
            }

            if (retryMode is LangfuseHttpRetryMode.Idempotent
                && failure!.IsTransient
                && attempt < _maxAttempts)
            {
                _health.RecordRetry(GetRetryReason(failure));
                await DelayBeforeRetryAsync(failure, attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw failure!;
        }
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        Uri uri,
        byte[]? serializedPayload)
    {
        var request = new HttpRequestMessage(method, uri);
        if (serializedPayload is not null)
        {
            request.Content = new ByteArrayContent(serializedPayload);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return request;
    }

    private async Task DelayBeforeRetryAsync(
        LangfuseHttpException failure,
        int failedAttempt,
        CancellationToken cancellationToken)
    {
        var delay = failure.RetryAfter ?? GetExponentialDelay(failedAttempt);
        if (delay > _maxRetryDelay)
        {
            delay = _maxRetryDelay;
        }

        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
    }

    private TimeSpan GetExponentialDelay(int failedAttempt)
    {
        var delay = _initialRetryDelay;
        for (var attempt = 1; attempt < failedAttempt && delay < _maxRetryDelay; attempt++)
        {
            if (delay > TimeSpan.FromTicks(_maxRetryDelay.Ticks / 2))
            {
                return _maxRetryDelay;
            }

            delay += delay;
        }

        return delay;
    }

    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (response.Headers.RetryAfter?.Date is not { } date)
        {
            return null;
        }

        var delay = date - _timeProvider.GetUtcNow();
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is
            HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static LangfuseRetryReason GetRetryReason(LangfuseHttpException failure)
    {
        if (failure.StatusCode is HttpStatusCode.TooManyRequests)
        {
            return LangfuseRetryReason.RateLimited;
        }

        if (failure.IsTimeout)
        {
            return LangfuseRetryReason.TimedOut;
        }

        return failure.StatusCode is null
            ? LangfuseRetryReason.Transport
            : LangfuseRetryReason.TransientServer;
    }

    private static async Task<string> ReadFailureBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
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
