using System.Diagnostics;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseTraceAttributeProcessorTests
{
    [Fact]
    public void OnStart_CopiesBaggageToTags_WithoutOverwritingExistingTags()
    {
        using var scope = StartActivity("op", out var activity);
        activity.SetBaggage("session.id", "run-9");
        activity.SetTag("user.id", "preset");
        activity.SetBaggage("user.id", "from-baggage");

        new LangfuseTraceAttributeProcessor().OnStart(activity);

        Assert.Equal("run-9", activity.GetTagItem("session.id"));
        Assert.Equal("preset", activity.GetTagItem("user.id"));
    }

    [Theory]
    [InlineData("agent.chat", "generation")]
    [InlineData("agent.chat.stream", "generation")]
    [InlineData("agent.tool search_web", "span")]
    [InlineData("agent.tool fetch_weather", "span")]
    public void OnStart_SetsObservationType(string operationName, string expectedType)
    {
        using var scope = StartActivity(operationName, out var activity);

        new LangfuseTraceAttributeProcessor().OnStart(activity);

        Assert.Equal(expectedType, activity.GetTagItem("langfuse.observation.type"));
    }

    [Theory]
    [InlineData("eval: cached-summary")]
    [InlineData("agent.chatter")] // not an exact match for agent.chat — must NOT become a generation
    [InlineData("some.other.span")]
    public void OnStart_LeavesObservationTypeUnsetForNonAgentSpans(string operationName)
    {
        using var scope = StartActivity(operationName, out var activity);

        new LangfuseTraceAttributeProcessor().OnStart(activity);

        Assert.Null(activity.GetTagItem("langfuse.observation.type"));
    }

    [Fact]
    public void OnEnd_WritesUsageDetailsJsonFromGenAiTags()
    {
        using var scope = StartActivity("agent.chat", out var activity);
        activity.SetTag("gen_ai.usage.input_tokens", 1000);
        activity.SetTag("gen_ai.usage.output_tokens", 200);
        activity.SetTag("gen_ai.usage.cached_input_tokens", 500);
        activity.SetTag("gen_ai.usage.reasoning_tokens", 50);

        new LangfuseTraceAttributeProcessor().OnEnd(activity);

        var raw = Assert.IsType<string>(activity.GetTagItem("langfuse.observation.usage_details"));
        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        Assert.Equal(1000, root.GetProperty("input").GetInt64());
        Assert.Equal(200, root.GetProperty("output").GetInt64());
        Assert.Equal(500, root.GetProperty("cache_read_input_tokens").GetInt64());
        Assert.Equal(50, root.GetProperty("reasoning_tokens").GetInt64());
        Assert.Equal(1200, root.GetProperty("total").GetInt64());
    }

    [Fact]
    public void OnEnd_WithoutUsageTags_DoesNotWriteUsageDetails()
    {
        using var scope = StartActivity("agent.tool search", out var activity);

        new LangfuseTraceAttributeProcessor().OnEnd(activity);

        Assert.Null(activity.GetTagItem("langfuse.observation.usage_details"));
    }

    private static ActivityListener StartActivity(string operationName, out Activity activity)
    {
        var source = new ActivitySource($"test-{Guid.NewGuid():N}");
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        activity = source.StartActivity(operationName)
            ?? throw new InvalidOperationException("Activity was not sampled.");
        return listener;
    }
}
