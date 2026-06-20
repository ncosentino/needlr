namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// One filter in a <see cref="LangfuseMetricsQuery"/>.
/// </summary>
/// <param name="Column">The column to filter (for example <c>name</c>, <c>environment</c>).</param>
/// <param name="Operator">The operator (for example <c>=</c>).</param>
/// <param name="Value">The value to compare against.</param>
/// <param name="Type">The filter value type. Defaults to <c>string</c>.</param>
public sealed record LangfuseMetricsFilter(string Column, string Operator, object Value, string Type = "string");
