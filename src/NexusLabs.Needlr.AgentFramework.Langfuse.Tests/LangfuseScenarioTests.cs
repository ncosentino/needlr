using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

using Moq;
using Moq.Protected;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseScenarioTests
{
    [Fact]
    public void BeginScenario_SetsLangfuseTraceLevelAttributesOnRootSpan()
    {
        using var listener = StartListener();
        var recorder = new LangfuseScoreRecorder(CreateApiClient(out _), StrictSink(), normalizeNames: false);

        using var scenario = new LangfuseScenario(
            recorder,
            name: "trip-planner",
            sessionId: "run-1",
            userId: "user-9",
            tags: ["happy-path", "regression"],
            metadata: new Dictionary<string, string> { ["dataset"] = "v1" });

        var activity = scenario.Activity;
        Assert.NotNull(activity);
        Assert.Equal("trip-planner", activity!.GetTagItem("langfuse.trace.name"));
        Assert.Equal("run-1", activity.GetTagItem("langfuse.session.id"));
        Assert.Equal("user-9", activity.GetTagItem("langfuse.user.id"));
        Assert.Equal("v1", activity.GetTagItem("langfuse.trace.metadata.dataset"));

        var tags = Assert.IsType<string[]>(activity.GetTagItem("langfuse.trace.tags"));
        Assert.Equal(["happy-path", "regression"], tags);

        Assert.Equal("run-1", activity.GetBaggageItem("session.id"));
        Assert.Equal("user-9", activity.GetBaggageItem("user.id"));
        Assert.False(string.IsNullOrEmpty(scenario.TraceId));
    }

    [Fact]
    public async Task RecordScoreAsync_PostsScoreForScenarioTrace()
    {
        using var listener = StartListener();
        var recorder = new LangfuseScoreRecorder(CreateApiClient(out var bodies), StrictSink(), normalizeNames: false);

        using var scenario = new LangfuseScenario(recorder, "scenario", null, null, null, null);
        var traceId = scenario.TraceId;

        await scenario.RecordScoreAsync("relevance", 0.75, "looks good", TestContext.Current.CancellationToken);

        var body = Assert.Single(bodies);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(traceId, json.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("relevance", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(0.75, json.RootElement.GetProperty("value").GetDouble());
        Assert.Equal("NUMERIC", json.RootElement.GetProperty("dataType").GetString());
    }

    [Fact]
    public async Task RecordEvaluationAsync_MapsEachMetricTypeToAScore()
    {
        using var listener = StartListener();
        var recorder = new LangfuseScoreRecorder(CreateApiClient(out var bodies), StrictSink(), normalizeNames: false);

        using var scenario = new LangfuseScenario(recorder, "scenario", null, null, null, null);

        var result = new EvaluationResult(
            new NumericMetric("Total Tokens", value: 1200),
            new BooleanMetric("All Tool Calls Succeeded", value: true),
            new StringMetric("Termination Mode", value: "completed"),
            new NumericMetric("Unset Metric", value: null));

        await scenario.RecordEvaluationAsync(result, TestContext.Current.CancellationToken);

        Assert.Equal(3, bodies.Count);

        var byName = bodies
            .Select(b => JsonDocument.Parse(b).RootElement)
            .ToDictionary(e => e.GetProperty("name").GetString()!, e => e);

        Assert.Equal("NUMERIC", byName["Total Tokens"].GetProperty("dataType").GetString());
        Assert.Equal(1200, byName["Total Tokens"].GetProperty("value").GetDouble());

        Assert.Equal("BOOLEAN", byName["All Tool Calls Succeeded"].GetProperty("dataType").GetString());
        Assert.Equal(1, byName["All Tool Calls Succeeded"].GetProperty("value").GetDouble());

        Assert.Equal("CATEGORICAL", byName["Termination Mode"].GetProperty("dataType").GetString());
        Assert.Equal("completed", byName["Termination Mode"].GetProperty("value").GetString());

        Assert.False(byName.ContainsKey("Unset Metric"));
    }

    [Fact]
    public async Task RecordScoreAsync_NonFatalMode_DoesNotThrowAndRecordsFailure()
    {
        using var listener = StartListener();
        LangfuseScoreError? captured = null;
        var sink = new LangfuseScoreFailureSink(LangfuseScoreFailureMode.NonFatal, e => captured = e);
        var recorder = new LangfuseScoreRecorder(CreateFailingApiClient(), sink, normalizeNames: false);

        using var scenario = new LangfuseScenario(recorder, "scenario", null, null, null, null);

        await scenario.RecordScoreAsync("relevance", 0.5, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, sink.FailedCount);
        Assert.NotNull(captured);
        Assert.Equal("relevance", captured!.ScoreName);
        Assert.Equal(scenario.TraceId, captured.TraceId);
    }

    [Fact]
    public async Task RecordScoreAsync_StrictMode_Throws()
    {
        using var listener = StartListener();
        var sink = new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null);
        var recorder = new LangfuseScoreRecorder(CreateFailingApiClient(), sink, normalizeNames: false);

        using var scenario = new LangfuseScenario(recorder, "scenario", null, null, null, null);

        await Assert.ThrowsAsync<LangfuseException>(() =>
            scenario.RecordScoreAsync("relevance", 0.5, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, sink.FailedCount);
    }

    [Fact]
    public async Task ChildSpans_InheritOnlyTheirOwnScenarioBaggage_UnderParallelScenarios()
    {
        using var listener = StartListener();
        var recorder = new LangfuseScoreRecorder(CreateApiClient(out _), StrictSink(), normalizeNames: false);
        var processor = new LangfuseTraceAttributeProcessor();

        async Task<(string? Session, string? User)> RunScenario(string id)
        {
            await Task.Yield();
            using var scenario = new LangfuseScenario(
                recorder, $"scenario-{id}", sessionId: id, userId: $"user-{id}", tags: null, metadata: null);

            await Task.Delay(15);
            using var child = LangfuseActivitySource.Source.StartActivity("agent.tool work")!;
            processor.OnStart(child);
            await Task.Delay(15);

            return (child.GetTagItem("session.id") as string, child.GetTagItem("user.id") as string);
        }

        var results = await Task.WhenAll(RunScenario("A"), RunScenario("B"));

        Assert.Contains(("A", "user-A"), results);
        Assert.Contains(("B", "user-B"), results);
    }

    private static LangfuseScoreFailureSink StrictSink() => new(LangfuseScoreFailureMode.Strict, null);

    private static ActivityListener StartListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == LangfuseActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static LangfuseScoreApiClient CreateApiClient(out List<string> capturedBodies)
    {
        var bodies = new List<string>();
        capturedBodies = bodies;

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                bodies.Add(await request.Content!.ReadAsStringAsync(token));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var httpClient = new HttpClient(handler.Object);
        return new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://cloud.langfuse.com/api/public/scores"),
            "Basic cGs6c2s=");
    }

    private static LangfuseScoreApiClient CreateFailingApiClient()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            });

        var httpClient = new HttpClient(handler.Object);
        return new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://cloud.langfuse.com/api/public/scores"),
            "Basic cGs6c2s=");
    }
}
