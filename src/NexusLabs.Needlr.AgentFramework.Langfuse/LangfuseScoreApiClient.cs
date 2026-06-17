using System.Net.Http.Json;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Posts scores to the Langfuse public Scores API (<c>POST /api/public/scores</c>) using HTTP
/// Basic authentication. This is the low-level transport; mapping and failure handling live in
/// <see cref="LangfuseScoreRecorder"/>.
/// </summary>
/// <remarks>
/// A score may be ingested before its trace exists; Langfuse links the two by trace id once the
/// trace is received. The underlying <see cref="HttpClient"/> is owned by the caller and disposed
/// with it.
/// </remarks>
internal sealed class LangfuseScoreApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _scoresEndpoint;

    public LangfuseScoreApiClient(HttpClient httpClient, Uri scoresEndpoint, string authorizationHeaderValue)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(scoresEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationHeaderValue);

        _httpClient = httpClient;
        _scoresEndpoint = scoresEndpoint;

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

    /// <summary>
    /// Sends a single score to Langfuse.
    /// </summary>
    /// <param name="score">The score to ingest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="LangfuseException">The request failed or returned a non-success status.</exception>
    public async Task CreateAsync(LangfuseScore score, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(score);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .PostAsJsonAsync(_scoresEndpoint, score, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LangfuseException(
                $"Failed to send score '{score.Name}' to Langfuse at '{_scoresEndpoint}'.", ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            throw new LangfuseException(
                $"Langfuse rejected score '{score.Name}' with status {(int)response.StatusCode} " +
                $"({response.ReasonPhrase}): {body}");
        }
    }
}
