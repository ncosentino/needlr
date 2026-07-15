namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Posts scores through the shared <see cref="LangfuseApiClient"/> and enables retries only when a
/// caller-supplied score id makes the complete request provider-idempotent.
/// </summary>
/// <remarks>
/// A score may be ingested before its trace exists; Langfuse links the two by trace id once the
/// trace is received.
/// </remarks>
[DoNotAutoRegister]
internal sealed class LangfuseScoreApiClient
{
    private readonly LangfuseApiClient _apiClient;

    public LangfuseScoreApiClient(LangfuseApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <summary>
    /// Sends a single score to Langfuse.
    /// </summary>
    /// <param name="score">The score to ingest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="LangfuseException">The request failed or returned a non-success status.</exception>
    public async Task<string?> CreateAsync(LangfuseScore score, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(score);

        LangfuseCreateScoreResponse? response;
        try
        {
            if (score.Id is null)
            {
                response = await _apiClient
                    .PostAsync<LangfuseScore, LangfuseCreateScoreResponse>(
                        "api/public/scores",
                        score,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                response = await _apiClient
                    .PostIdempotentAsync<LangfuseScore, LangfuseCreateScoreResponse>(
                        "api/public/scores",
                        score,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new LangfuseException(
                $"Langfuse returned an invalid response after accepting score '{score.Name}'.",
                ex);
        }

        if (response is null || string.IsNullOrWhiteSpace(response.Id))
        {
            throw new LangfuseException(
                $"Langfuse returned an invalid response after accepting score '{score.Name}'.");
        }

        if (!string.IsNullOrWhiteSpace(score.Id)
            && !string.Equals(response.Id, score.Id, StringComparison.Ordinal))
        {
            throw new LangfuseException(
                $"Langfuse returned score id '{response.Id}' after accepting caller-supplied id '{score.Id}'.");
        }

        return response.Id;
    }
}
