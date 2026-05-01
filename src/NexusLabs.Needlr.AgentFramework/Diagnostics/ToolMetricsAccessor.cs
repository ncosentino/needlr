using System.Collections.Concurrent;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IToolMetricsAccessor"/>.
/// The diagnostics function-calling middleware establishes and clears the metrics dict
/// per tool invocation.
/// </summary>
/// <remarks>
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> so that tool functions which
/// fan out concurrent work via <c>Task.WhenAll</c> can safely call
/// <see cref="AttachMetric"/> from multiple threads.
/// </remarks>
internal sealed class ToolMetricsAccessor : IToolMetricsAccessor
{
    internal static readonly AsyncLocal<ConcurrentDictionary<string, object?>?> CurrentToolMetrics = new();

    /// <inheritdoc />
    public void AttachMetric(string key, object? value)
    {
        if (CurrentToolMetrics.Value is { } metrics)
        {
            metrics[key] = value;
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?>? GetCurrentMetrics() =>
        CurrentToolMetrics.Value;
}
