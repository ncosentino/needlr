using System.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseProcessorContextTests
{
    [Fact]
    public void OnStart_WithEnvironmentAndRelease_SetsBothAttributes()
    {
        var processor = new LangfuseTraceAttributeProcessor(environment: "ci", release: "abc123");
        using var activity = new Activity("agent.chat");

        processor.OnStart(activity);

        Assert.Equal("ci", activity.GetTagItem("langfuse.environment"));
        Assert.Equal("abc123", activity.GetTagItem("langfuse.release"));
    }

    [Fact]
    public void OnStart_WithoutContext_DoesNotSetAttributes()
    {
        var processor = new LangfuseTraceAttributeProcessor();
        using var activity = new Activity("agent.chat");

        processor.OnStart(activity);

        Assert.Null(activity.GetTagItem("langfuse.environment"));
        Assert.Null(activity.GetTagItem("langfuse.release"));
    }

    [Fact]
    public void OnStart_DoesNotOverrideExistingEnvironment()
    {
        var processor = new LangfuseTraceAttributeProcessor(environment: "ci", release: null);
        using var activity = new Activity("agent.chat");
        activity.SetTag("langfuse.environment", "staging");

        processor.OnStart(activity);

        Assert.Equal("staging", activity.GetTagItem("langfuse.environment"));
    }

    [Fact]
    public void OnStart_BlankContext_IsTreatedAsUnset()
    {
        var processor = new LangfuseTraceAttributeProcessor(environment: "   ", release: "");
        using var activity = new Activity("agent.chat");

        processor.OnStart(activity);

        Assert.Null(activity.GetTagItem("langfuse.environment"));
        Assert.Null(activity.GetTagItem("langfuse.release"));
    }
}
