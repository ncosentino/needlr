namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseMetricsClient"/> returned when Langfuse is not configured. Queries
/// return empty results so calling code never needs to branch on configuration state.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseMetricsClient : ILangfuseMetricsClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task<LangfuseMetricsResult> QueryAsync(LangfuseMetricsQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LangfuseMetricsResult([]));

    /// <inheritdoc />
    public Task<double?> GetScoreAverageAsync(
        string scoreName,
        DateTimeOffset fromTimestamp,
        DateTimeOffset toTimestamp,
        string? environment = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<double?>(null);
}
