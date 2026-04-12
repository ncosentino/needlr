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
    public void AgentMetrics_ActivitySource_UsesFrameworkName()
    {
        using var metrics = new AgentMetrics();
        Assert.Equal(AgentMetrics.ActivitySourceName, metrics.ActivitySource.Name);
        Assert.Equal("NexusLabs.Needlr.AgentFramework", metrics.ActivitySource.Name);
    }

    [Fact]
    public void ActivitySource_CreateActivity_EmitsWhenListenerRegistered()
    {
        using var metrics = new AgentMetrics();
        var activities = new List<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentMetrics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = metrics.ActivitySource.StartActivity("test.operation", ActivityKind.Internal))
        {
            activity?.SetTag("test.key", "test.value");
        }

        Assert.Single(activities);
        Assert.Equal("test.operation", activities[0].OperationName);
        Assert.Equal(ActivityKind.Internal, activities[0].Kind);
        Assert.Equal("test.value", activities[0].GetTagItem("test.key"));
    }

    [Fact]
    public void ActivitySource_WithoutListener_ReturnsNullActivity()
    {
        using var metrics = new AgentMetrics();

        // No listener registered — StartActivity returns null, which is the
        // standard OpenTelemetry behavior. The middleware's null-conditional
        // calls (activity?.SetTag) are no-ops.
        var activity = metrics.ActivitySource.StartActivity("unlistened.op");
        Assert.Null(activity);
    }
}
