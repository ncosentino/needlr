using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseScoreRecorderTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task NormalizeScoreNames_ConvertsToSnakeCase()
    {
        var recorder = CreateRecorder(out var bodies, normalizeNames: true);

        await recorder.RecordNumericAsync(
            "trace-1",
            "All Tool Calls Succeeded",
            1.0,
            options: null,
            _cancellationToken);

        var body = Assert.Single(bodies);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("all_tool_calls_succeeded", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task WithoutNormalization_SendsNameVerbatim()
    {
        var recorder = CreateRecorder(out var bodies, normalizeNames: false);

        await recorder.RecordNumericAsync(
            "trace-1",
            "All Tool Calls Succeeded",
            1.0,
            options: null,
            _cancellationToken);

        var body = Assert.Single(bodies);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("All Tool Calls Succeeded", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task RecordNumericAsync_WithStableOptions_SerializesIdAndComment()
    {
        var recorder = CreateRecorder(out var bodies, normalizeNames: false);

        await recorder.RecordNumericAsync(
            "trace-1",
            "quality",
            0.9,
            new LangfuseScoreOptions
            {
                Id = "run-42:case-7:quality",
                Comment = "stable publication",
            },
            _cancellationToken);

        var body = Assert.Single(bodies);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("run-42:case-7:quality", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("stable publication", json.RootElement.GetProperty("comment").GetString());
        Assert.False(json.RootElement.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task RecordEvaluationAsync_UsesStableIdProvider()
    {
        var recorder = CreateRecorder(out var bodies, normalizeNames: false);
        var evaluation = new EvaluationResult(
            new NumericMetric("accuracy", 0.9),
            new BooleanMetric("passed", true));

        await recorder.RecordEvaluationAsync(
            "trace-1",
            evaluation,
            new LangfuseEvaluationScoreOptions
            {
                ScoreIdProvider = metric => $"run-42:case-7:{metric.Name}",
            },
            _cancellationToken);

        Assert.Equal(2, bodies.Count);
        using var first = JsonDocument.Parse(bodies[0]);
        using var second = JsonDocument.Parse(bodies[1]);
        Assert.Equal("run-42:case-7:accuracy", first.RootElement.GetProperty("id").GetString());
        Assert.Equal("run-42:case-7:passed", second.RootElement.GetProperty("id").GetString());
        Assert.False(first.RootElement.TryGetProperty("timestamp", out _));
        Assert.False(second.RootElement.TryGetProperty("timestamp", out _));
    }

    private static LangfuseScoreRecorder CreateRecorder(
        out List<string> capturedBodies,
        bool normalizeNames)
    {
        var bodies = new List<string>();
        capturedBodies = bodies;

        var handler = new DelegateHttpMessageHandler(
            async (request, token) =>
            {
                bodies.Add(await request.Content!.ReadAsStringAsync(token));
                return LangfuseHttpStub.ScoreAccepted(request);
            });

        var apiClient = LangfuseTestFactory.CreateScoreApiClient(
            new HttpClient(handler),
            new Uri("https://cloud.langfuse.com/"),
            "Basic cGs6c2s=");
        var sink = new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null);
        return new LangfuseScoreRecorder(
            apiClient,
            sink,
            normalizeNames);
    }
}
