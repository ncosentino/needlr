namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Diagnostics for a single tool/function invocation within an agent run.
/// </summary>
/// <remarks>
/// <para>
/// Tool calls are captured by the diagnostics function-calling middleware and ordered
/// by their <paramref name="Sequence"/> number, which is reserved before the call begins.
/// This ensures parallel tool calls are ordered by invocation time, not completion time.
/// </para>
/// <para>
/// Custom metrics (e.g., cache hit/miss, provider name, byte counts) can be attached
/// via <see cref="IToolMetricsAccessor.AttachMetric"/> during tool execution.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// foreach (var tool in diagnostics.ToolCalls)
/// {
///     Console.WriteLine($"{tool.ToolName}: {tool.Duration.TotalMilliseconds}ms " +
///         $"({(tool.Succeeded ? "ok" : tool.ErrorMessage)})");
///     if (tool.CustomMetrics is { Count: > 0 })
///         foreach (var (k, v) in tool.CustomMetrics)
///             Console.WriteLine($"  {k} = {v}");
/// }
/// </code>
/// </example>
/// <param name="Sequence">Zero-based invocation order within the agent run. Reserved before async execution begins.</param>
/// <param name="ToolName">The name of the tool/function that was called (e.g., <c>"ReadFile"</c>, <c>"WebSearch"</c>).</param>
/// <param name="Duration">Wall-clock time for the tool execution.</param>
/// <param name="Succeeded">Whether the tool returned a result without throwing.</param>
/// <param name="ErrorMessage">The exception message if the tool threw; <see langword="null"/> on success.</param>
/// <param name="StartedAt">UTC timestamp when the tool invocation began.</param>
/// <param name="CompletedAt">UTC timestamp when the tool invocation finished.</param>
/// <param name="CustomMetrics">Arbitrary key-value pairs attached by the tool implementation via <see cref="IToolMetricsAccessor"/>. <see langword="null"/> if no metrics were recorded.</param>
public sealed record ToolCallDiagnostics(
    int Sequence,
    string ToolName,
    TimeSpan Duration,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyDictionary<string, object?>? CustomMetrics)
{
    /// <summary>
    /// The name of the agent that triggered this tool call, or <see langword="null"/>
    /// if the agent name was not available. Used to attribute tool calls to the
    /// correct agent in multi-agent workflows where diagnostics are aggregated.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// The arguments passed to the tool invocation, keyed by parameter name.
    /// <see langword="null"/> if the tool took no arguments or capture was unavailable.
    /// Captured losslessly to enable post-hoc replay and tool-call-accuracy evaluation.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }

    /// <summary>
    /// The value returned by the tool invocation, or <see langword="null"/> if the
    /// tool returned <see langword="null"/> or the call failed. Captured losslessly
    /// (the actual object returned, not a serialized form) to enable post-hoc
    /// evaluation and replay.
    /// </summary>
    public object? Result { get; init; }
}
