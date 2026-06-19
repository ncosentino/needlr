namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// The Langfuse Metrics API view to query. Determines which measures and dimensions are available.
/// </summary>
public enum LangfuseMetricsView
{
    /// <summary>Observation-level data (spans, generations, events): latency, tokens, cost, counts.</summary>
    Observations = 0,

    /// <summary>Numeric and boolean score data.</summary>
    ScoresNumeric = 1,

    /// <summary>Categorical score data.</summary>
    ScoresCategorical = 2,
}
