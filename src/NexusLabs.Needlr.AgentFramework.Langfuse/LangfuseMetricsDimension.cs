namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>One dimension entry in the Metrics API wire payload (<c>{ "field": "name" }</c>).</summary>
internal sealed record LangfuseMetricsDimension(string Field);
