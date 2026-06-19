namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// One metric in a <see cref="LangfuseMetricsQuery"/>: a measure plus an aggregation. The result
/// column is named <c>{Aggregation}_{Measure}</c> (for example <c>avg_value</c>, <c>sum_totalCost</c>).
/// </summary>
/// <param name="Measure">The measure to aggregate (for example <c>value</c>, <c>totalCost</c>, <c>count</c>, <c>latency</c>).</param>
/// <param name="Aggregation">The aggregation (for example <c>avg</c>, <c>sum</c>, <c>count</c>, <c>p95</c>).</param>
public sealed record LangfuseMetric(string Measure, string Aggregation);
