using System.Diagnostics;
using System.Net;
using System.Text.Json;

using OpenTelemetry;
using OpenTelemetry.Trace;

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

    [Fact]
    public void SetPrompt_WithFetchedPrompt_LinksByNameAndVersion()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var scenario = new LangfuseScenario(LangfuseTestFactory.OkScoreRecorder(), "s", null, null, null, null);
        scenario.SetPrompt(new LangfusePrompt { Name = "trip-planner", Version = 4, Type = "text" });

        var processor = new LangfuseTraceAttributeProcessor();
        using var child = LangfuseActivitySource.Source.StartActivity("agent.chat")!;
        processor.OnStart(child);

        Assert.Equal("trip-planner", child.GetTagItem("langfuse.observation.prompt.name"));
        Assert.Equal(4, child.GetTagItem("langfuse.observation.prompt.version"));
    }

    [Fact]
    public void TraceContext_PropagatesSupportedAttributesWithoutExportingInheritedBaggage()
    {
        using var external = new Activity("external").Start();
        external.SetBaggage("authorization", "secret");
        using var listener = LangfuseTestFactory.StartListener();
        using var scenario = new LangfuseScenario(
            LangfuseTestFactory.OkScoreRecorder(),
            "evaluation-case",
            sessionId: "session-1",
            userId: "user-1",
            tags: ["regression", "parallel"],
            metadata: new Dictionary<string, string> { ["dataset"] = "v2" });
        scenario.SetVersion("v3");

        var processor = new LangfuseTraceAttributeProcessor();
        using var child = LangfuseActivitySource.Source.StartActivity("agent.tool work")!;
        processor.OnStart(child);

        Assert.Equal("evaluation-case", child.GetTagItem("langfuse.trace.name"));
        Assert.Equal("session-1", child.GetTagItem("session.id"));
        Assert.Equal("user-1", child.GetTagItem("user.id"));
        Assert.Equal("v3", child.GetTagItem("langfuse.version"));
        Assert.Equal("v2", child.GetTagItem("langfuse.trace.metadata.dataset"));
        Assert.Equal(
            ["regression", "parallel"],
            Assert.IsType<string[]>(child.GetTagItem("langfuse.trace.tags")));
        Assert.Null(child.GetTagItem("authorization"));
    }

    [Fact]
    public void NestedScenario_ReplacesOuterTraceContextInsteadOfMergingIt()
    {
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(LangfuseActivitySource.Name)
            .AddProcessor(new LangfuseTraceAttributeProcessor())
            .Build();
        using var outer = new LangfuseScenario(
            LangfuseTestFactory.OkScoreRecorder(),
            "outer",
            sessionId: "outer-session",
            userId: "outer-user",
            tags: ["outer-a", "outer-b"],
            metadata: new Dictionary<string, string> { ["outer"] = "value" });
        outer.SetVersion("outer-version");

        using var inner = new LangfuseScenario(
            LangfuseTestFactory.OkScoreRecorder(),
            "inner",
            sessionId: "inner-session",
            userId: null,
            tags: ["inner"],
            metadata: new Dictionary<string, string> { ["inner"] = "value" });

        Assert.NotEqual(outer.TraceId, inner.TraceId);
        var innerActivity = inner.Activity!;
        Assert.Equal("inner", innerActivity.GetTagItem("langfuse.trace.name"));
        Assert.Equal("inner-session", innerActivity.GetTagItem("langfuse.session.id"));
        Assert.Null(innerActivity.GetTagItem("session.id"));
        Assert.Null(innerActivity.GetTagItem("user.id"));
        Assert.Null(innerActivity.GetTagItem("langfuse.version"));
        Assert.Equal("value", innerActivity.GetTagItem("langfuse.trace.metadata.inner"));
        Assert.Null(innerActivity.GetTagItem("langfuse.trace.metadata.outer"));
        Assert.Equal(["inner"], Assert.IsType<string[]>(innerActivity.GetTagItem("langfuse.trace.tags")));
        using var child = LangfuseActivitySource.Source.StartActivity("agent.tool nested")!;

        Assert.Equal("inner", child.GetTagItem("langfuse.trace.name"));
        Assert.Equal("inner-session", child.GetTagItem("session.id"));
        Assert.Null(child.GetTagItem("user.id"));
        Assert.Null(child.GetTagItem("langfuse.version"));
        Assert.Equal("value", child.GetTagItem("langfuse.trace.metadata.inner"));
        Assert.Null(child.GetTagItem("langfuse.trace.metadata.outer"));
        Assert.Equal(["inner"], Assert.IsType<string[]>(child.GetTagItem("langfuse.trace.tags")));
    }

    [Fact]
    public void NestedScenario_DisposeRestoresOuterActivity()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var outer = new LangfuseScenario(
            LangfuseTestFactory.OkScoreRecorder(),
            "outer",
            sessionId: null,
            userId: null,
            tags: null,
            metadata: null);

        Assert.Same(outer.Activity, Activity.Current);
        using (var inner = new LangfuseScenario(
            LangfuseTestFactory.OkScoreRecorder(),
            "inner",
            sessionId: null,
            userId: null,
            tags: null,
            metadata: null))
        {
            Assert.Same(inner.Activity, Activity.Current);
            Assert.NotEqual(outer.TraceId, inner.TraceId);
        }

        Assert.Same(outer.Activity, Activity.Current);
    }
}
