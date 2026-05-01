using System.Collections.Concurrent;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ToolMetricsAccessorTests
{
    [Fact]
    public void AttachMetric_WithoutScope_IsNoOp()
    {
        var accessor = new ToolMetricsAccessor();

        // Should not throw
        accessor.AttachMetric("key", "value");
    }

    [Fact]
    public void GetCurrentMetrics_WithoutScope_ReturnsNull()
    {
        var accessor = new ToolMetricsAccessor();

        Assert.Null(accessor.GetCurrentMetrics());
    }

    [Fact]
    public void AttachMetric_WithScope_IsVisible()
    {
        var accessor = new ToolMetricsAccessor();

        // Simulate middleware establishing the scope
        var dict = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ToolMetricsAccessor.CurrentToolMetrics.Value = dict;

        try
        {
            accessor.AttachMetric("cache_hit", true);
            accessor.AttachMetric("provider", "brave");

            var metrics = accessor.GetCurrentMetrics();
            Assert.NotNull(metrics);
            Assert.Equal(true, metrics!["cache_hit"]);
            Assert.Equal("brave", metrics["provider"]);
        }
        finally
        {
            ToolMetricsAccessor.CurrentToolMetrics.Value = null;
        }
    }

    [Fact]
    public void AttachMetric_OverwritesPreviousValue()
    {
        var dict = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ToolMetricsAccessor.CurrentToolMetrics.Value = dict;

        try
        {
            var accessor = new ToolMetricsAccessor();
            accessor.AttachMetric("count", 1);
            accessor.AttachMetric("count", 2);

            Assert.Equal(2, accessor.GetCurrentMetrics()!["count"]);
        }
        finally
        {
            ToolMetricsAccessor.CurrentToolMetrics.Value = null;
        }
    }

    [Fact]
    public void AttachMetric_CaseInsensitive()
    {
        var dict = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ToolMetricsAccessor.CurrentToolMetrics.Value = dict;

        try
        {
            var accessor = new ToolMetricsAccessor();
            accessor.AttachMetric("Provider", "brave");

            Assert.Equal("brave", accessor.GetCurrentMetrics()!["provider"]);
        }
        finally
        {
            ToolMetricsAccessor.CurrentToolMetrics.Value = null;
        }
    }

    [Fact]
    public async Task AttachMetric_ConcurrentAccess_DoesNotCorrupt()
    {
        var accessor = new ToolMetricsAccessor();
        const int taskCount = 50;

        // Simulate middleware establishing the scope
        var dict = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ToolMetricsAccessor.CurrentToolMetrics.Value = dict;

        try
        {
            // Simulate Task.WhenAll inside a tool — all child tasks share the
            // same AsyncLocal value and call AttachMetric concurrently.
            var tasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(() =>
            {
                accessor.AttachMetric($"metric_{i}", i);
                accessor.AttachMetric($"metric_{i}_extra", i * 10);
            }));

            await Task.WhenAll(tasks);

            var metrics = accessor.GetCurrentMetrics();
            Assert.NotNull(metrics);
            Assert.Equal(taskCount * 2, metrics!.Count);
        }
        finally
        {
            ToolMetricsAccessor.CurrentToolMetrics.Value = null;
        }
    }
}
