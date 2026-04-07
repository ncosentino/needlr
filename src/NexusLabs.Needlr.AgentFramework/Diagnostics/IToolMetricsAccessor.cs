namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Allows tools to attach custom domain-specific metrics during execution.
/// Metrics are collected by the diagnostics function-calling middleware and included in
/// <see cref="ToolCallDiagnostics.CustomMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// Tools inject this interface via DI and call <see cref="AttachMetric"/> during execution:
/// </para>
/// <code>
/// public class WebSearchTool(IToolMetricsAccessor metrics)
/// {
///     [AgentFunction]
///     public async Task&lt;string[]&gt; Search(string query)
///     {
///         metrics.AttachMetric("cache_hit", false);
///         metrics.AttachMetric("provider", "brave");
///         // ... search logic
///     }
/// }
/// </code>
/// <para>
/// If called outside a diagnostics scope (no middleware), <see cref="AttachMetric"/> is a no-op.
/// </para>
/// </remarks>
public interface IToolMetricsAccessor
{
    /// <summary>
    /// Attaches a custom metric to the current tool invocation. No-op if called outside
    /// a diagnostics scope.
    /// </summary>
    /// <param name="key">The metric key (case-insensitive).</param>
    /// <param name="value">The metric value.</param>
    void AttachMetric(string key, object? value);

    /// <summary>
    /// Gets the metrics dictionary for the current tool invocation, or <see langword="null"/>
    /// if called outside a tool invocation scope.
    /// </summary>
    IReadOnlyDictionary<string, object?>? GetCurrentMetrics();
}
