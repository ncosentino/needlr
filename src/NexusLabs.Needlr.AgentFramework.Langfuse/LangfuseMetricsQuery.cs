namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// A query against the Langfuse Metrics API (<c>GET /api/public/metrics</c>). Use it to read
/// aggregates back from Langfuse — for example to drive a CI quality gate from recorded eval scores.
/// </summary>
/// <remarks>
/// Langfuse ingests asynchronously, so freshly recorded data may take a few seconds to appear in
/// metrics. Flush and allow for ingestion lag before asserting on results.
/// </remarks>
public sealed record LangfuseMetricsQuery
{
    /// <summary>Gets the view to query.</summary>
    public required LangfuseMetricsView View { get; init; }

    /// <summary>Gets the measures and aggregations to compute.</summary>
    public required IReadOnlyList<LangfuseMetric> Metrics { get; init; }

    /// <summary>
    /// Gets the dimension field names to group by. Empty produces a single aggregate row.
    /// </summary>
    public IReadOnlyList<string> Dimensions { get; init; } = [];

    /// <summary>Gets the filters to apply.</summary>
    public IReadOnlyList<LangfuseMetricsFilter> Filters { get; init; } = [];

    /// <summary>Gets the inclusive start of the time window.</summary>
    public required DateTimeOffset FromTimestamp { get; init; }

    /// <summary>Gets the exclusive end of the time window.</summary>
    public required DateTimeOffset ToTimestamp { get; init; }
}
