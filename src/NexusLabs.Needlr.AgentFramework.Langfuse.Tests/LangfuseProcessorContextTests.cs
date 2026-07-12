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

    [Fact]
    public void OnStart_GenerationSpanWithPromptContext_StampsObservationPrompt()
    {
        var processor = new LangfuseTraceAttributeProcessor();
        using var activity = new Activity("agent.chat");
        SetPromptContext(activity, "trip-planner", 7);

        processor.OnStart(activity);

        Assert.Equal("trip-planner", activity.GetTagItem("langfuse.observation.prompt.name"));
        Assert.Equal(7, activity.GetTagItem("langfuse.observation.prompt.version"));
    }

    [Fact]
    public void OnStart_PromptNameOnly_StampsNameWithoutVersion()
    {
        var processor = new LangfuseTraceAttributeProcessor();
        using var activity = new Activity("agent.chat");
        SetPromptContext(activity, "trip-planner", version: null);

        processor.OnStart(activity);

        Assert.Equal("trip-planner", activity.GetTagItem("langfuse.observation.prompt.name"));
        Assert.Null(activity.GetTagItem("langfuse.observation.prompt.version"));
    }

    [Fact]
    public void OnStart_ToolSpanWithPromptContext_DoesNotStampPrompt()
    {
        var processor = new LangfuseTraceAttributeProcessor();
        using var activity = new Activity("agent.tool search");
        SetPromptContext(activity, "trip-planner", version: null);

        processor.OnStart(activity);

        Assert.Null(activity.GetTagItem("langfuse.observation.prompt.name"));
    }

    private static void SetPromptContext(Activity activity, string name, int? version) =>
        activity.SetCustomProperty(
            LangfuseTraceContext.ActivityPropertyName,
            new LangfuseTraceContext
            {
                Name = "scenario",
                PromptName = name,
                PromptVersion = version,
            });
}
