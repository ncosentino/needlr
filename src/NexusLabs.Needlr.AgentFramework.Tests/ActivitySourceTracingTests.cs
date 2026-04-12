using System.Diagnostics;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ActivitySourceTracingTests
{
    [Fact]
    public void AgentMetrics_ActivitySource_IsNotNull()
    {
        using var metrics = new AgentMetrics();
        Assert.NotNull(metrics.ActivitySource);
    }

    [Fact]
    public void AgentMetrics_DefaultName_UsesFrameworkName()
    {
        using var metrics = new AgentMetrics();
        Assert.Equal("NexusLabs.Needlr.AgentFramework", metrics.ActivitySource.Name);
    }

    [Fact]
    public void AgentMetrics_CustomMeterName_AppliedToActivitySource()
    {
        var options = new AgentFrameworkMetricsOptions { MeterName = "MyApp.Agents" };
        using var metrics = new AgentMetrics(options);
        Assert.Equal("MyApp.Agents", metrics.ActivitySource.Name);
    }

    [Fact]
    public void AgentMetrics_CustomActivitySourceName_OverridesMeterName()
    {
        var options = new AgentFrameworkMetricsOptions
        {
            MeterName = "MyApp.Agents",
            ActivitySourceName = "MyApp.Tracing"
        };
        using var metrics = new AgentMetrics(options);
        Assert.Equal("MyApp.Tracing", metrics.ActivitySource.Name);
    }

    [Fact]
    public void ActivitySource_CreateActivity_EmitsWhenListenerRegistered()
    {
        var options = new AgentFrameworkMetricsOptions { MeterName = "test.source" };
        using var metrics = new AgentMetrics(options);
        var activities = new List<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "test.source",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = metrics.ActivitySource.StartActivity("test.operation", ActivityKind.Internal))
        {
            activity?.SetTag("gen_ai.agent.name", "TestAgent");
        }

        Assert.Single(activities);
        Assert.Equal("test.operation", activities[0].OperationName);
        Assert.Equal("TestAgent", activities[0].GetTagItem("gen_ai.agent.name"));
    }

    [Fact]
    public void ActivitySource_WithoutListener_ReturnsNullActivity()
    {
        using var metrics = new AgentMetrics();
        var activity = metrics.ActivitySource.StartActivity("unlistened.op");
        Assert.Null(activity);
    }
}
