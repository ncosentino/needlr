namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable wire payload for the Metrics API <c>query</c> parameter. Property names are
/// projected to camelCase.
/// </summary>
internal sealed record LangfuseMetricsQueryPayload
{
    public required string View { get; init; }

    public required IReadOnlyList<LangfuseMetric> Metrics { get; init; }

    public required IReadOnlyList<LangfuseMetricsDimension> Dimensions { get; init; }

    public required IReadOnlyList<LangfuseMetricsFilter> Filters { get; init; }

    public required string FromTimestamp { get; init; }

    public required string ToTimestamp { get; init; }

    public static LangfuseMetricsQueryPayload From(LangfuseMetricsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new LangfuseMetricsQueryPayload
        {
            View = ToToken(query.View),
            Metrics = query.Metrics,
            Dimensions = [.. query.Dimensions.Select(d => new LangfuseMetricsDimension(d))],
            Filters = query.Filters,
            FromTimestamp = query.FromTimestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
            ToTimestamp = query.ToTimestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private static string ToToken(LangfuseMetricsView view) => view switch
    {
        LangfuseMetricsView.Observations => "observations",
        LangfuseMetricsView.ScoresNumeric => "scores-numeric",
        LangfuseMetricsView.ScoresCategorical => "scores-categorical",
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unknown Langfuse metrics view."),
    };
}
