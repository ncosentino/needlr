using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseScenarioContextTests
{
    [Fact]
    public void SetTracePublicVersionInputOutput_WriteLangfuseTags()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var scenario = new LangfuseScenario(LangfuseTestFactory.OkScoreRecorder(), "s", null, null, null, null);

        scenario.SetTracePublic();
        scenario.SetVersion("v3");
        scenario.SetInput("the prompt");
        scenario.SetOutput(new { answer = 42 });

        var activity = scenario.Activity!;
        Assert.True((bool)activity.GetTagItem("langfuse.trace.public")!);
        Assert.Equal("v3", activity.GetTagItem("langfuse.version"));
        Assert.Equal("the prompt", activity.GetTagItem("langfuse.trace.input"));
        Assert.Equal("{\"answer\":42}", activity.GetTagItem("langfuse.trace.output"));
    }

    [Fact]
    public async Task RecordSessionScoreAsync_WithSessionId_PostsSessionScore()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var scenario = new LangfuseScenario(
            LangfuseTestFactory.OkScoreRecorder(captured), "s", sessionId: "sess-1", null, null, null);

        await scenario.RecordSessionScoreAsync("resolved", true, cancellationToken: TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(Assert.Single(captured).Body!);
        Assert.Equal("sess-1", json.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal("resolved", json.RootElement.GetProperty("name").GetString());
        Assert.False(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task RecordSessionScoreAsync_WithoutSessionId_IsSkippedNonFatally()
    {
        using var listener = LangfuseTestFactory.StartListener();
        LangfuseScoreError? captured = null;
        var sink = new LangfuseScoreFailureSink(LangfuseScoreFailureMode.NonFatal, e => captured = e);
        using var httpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), []);
        var apiClient = new LangfuseScoreApiClient(httpClient, new Uri("https://lf.example/api/public/scores"), "Basic x");
        var recorder = new LangfuseScoreRecorder(apiClient, sink, normalizeNames: false);
        using var scenario = new LangfuseScenario(recorder, "s", sessionId: null, null, null, null);

        await scenario.RecordSessionScoreAsync("resolved", 1.0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, sink.FailedCount);
        Assert.NotNull(captured);
        Assert.Contains("no session id", captured!.Exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetPrompt_PropagatesToChildGenerationSpan()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var scenario = new LangfuseScenario(LangfuseTestFactory.OkScoreRecorder(), "s", null, null, null, null);
        scenario.SetPrompt("trip-planner", 3);

        var processor = new LangfuseTraceAttributeProcessor();
        using var child = LangfuseActivitySource.Source.StartActivity("agent.chat")!;
        processor.OnStart(child);

        Assert.Equal("trip-planner", child.GetTagItem("langfuse.observation.prompt.name"));
        Assert.Equal(3, child.GetTagItem("langfuse.observation.prompt.version"));
    }
}
