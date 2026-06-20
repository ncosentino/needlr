namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Reads aggregates back from Langfuse via the Metrics API, so recorded eval scores (and observation
/// metrics) can drive CI quality gates and dashboards from Langfuse as the source of truth.
/// </summary>
public interface ILangfuseMetricsClient
{
    /// <summary>
    /// Gets a value indicating whether metric queries are performed. <see langword="false"/> when
    /// Langfuse is not configured, in which case queries return empty results.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Runs a metrics query and returns the result rows.</summary>
    /// <param name="query">The query to run.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The query result (empty when disabled).</returns>
    Task<LangfuseMetricsResult> QueryAsync(LangfuseMetricsQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience over <see cref="QueryAsync"/>: the average of a numeric score over a time window,
    /// optionally scoped to an environment. Returns <see langword="null"/> when there is no matching
    /// data (or Langfuse is not configured).
    /// </summary>
    /// <param name="scoreName">The score name to average.</param>
    /// <param name="fromTimestamp">The inclusive start of the window.</param>
    /// <param name="toTimestamp">The exclusive end of the window.</param>
    /// <param name="environment">An optional environment to filter by.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The average score value, or <see langword="null"/>.</returns>
    Task<double?> GetScoreAverageAsync(
        string scoreName,
        DateTimeOffset fromTimestamp,
        DateTimeOffset toTimestamp,
        string? environment = null,
        CancellationToken cancellationToken = default);
}
