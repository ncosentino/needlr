using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseMetricsClient"/> backed by the shared <see cref="LangfuseApiClient"/>.
/// Serializes the query to the Metrics API <c>query</c> parameter and projects the response rows.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseMetricsClient : ILangfuseMetricsClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly LangfuseApiClient _apiClient;

    public LangfuseMetricsClient(LangfuseApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task<LangfuseMetricsResult> QueryAsync(LangfuseMetricsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var payload = LangfuseMetricsQueryPayload.From(query);
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var path = $"api/public/metrics?query={Uri.EscapeDataString(json)}";

        var response = await _apiClient
            .GetAsync<LangfuseMetricsResponse>(path, cancellationToken)
            .ConfigureAwait(false);

        var rows = response?.Data is { } data
            ? data.Select(d => (IReadOnlyDictionary<string, JsonElement>)d).ToList()
            : [];

        return new LangfuseMetricsResult(rows);
    }

    /// <inheritdoc />
    public async Task<double?> GetScoreAverageAsync(
        string scoreName,
        DateTimeOffset fromTimestamp,
        DateTimeOffset toTimestamp,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreName);

        var filters = new List<LangfuseMetricsFilter> { new("name", "=", scoreName) };
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filters.Add(new LangfuseMetricsFilter("environment", "=", environment));
        }

        var query = new LangfuseMetricsQuery
        {
            View = LangfuseMetricsView.ScoresNumeric,
            Metrics = [new LangfuseMetric("value", "avg")],
            Dimensions = [],
            Filters = filters,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp,
        };

        var result = await QueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.GetScalar("avg", "value");
    }
}
