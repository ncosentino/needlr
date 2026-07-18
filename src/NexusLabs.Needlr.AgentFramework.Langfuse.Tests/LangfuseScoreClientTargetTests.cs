using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseScoreClientTargetTests
{
    private static LangfuseScoreClient CreateClient(List<CapturedRequest> captured)
    {
        var httpClient = LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, captured);
        var apiClient = LangfuseTestFactory.CreateScoreApiClient(httpClient);
        var sink = new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null);
        var recorder = new LangfuseScoreRecorder(apiClient, sink, normalizeNames: false);
        return new LangfuseScoreClient(recorder, sink);
    }

    [Fact]
    public async Task RecordObservationScoreAsync_PostsTraceAndObservationId()
    {
        var captured = new List<CapturedRequest>();
        var client = CreateClient(captured);

        await client.RecordObservationScoreAsync(
            "trace-1",
            "obs-2",
            "tool_correct",
            true,
            new LangfuseScoreOptions { Comment = "ok" },
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(Assert.Single(captured).Body!);
        Assert.Equal("trace-1", json.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("obs-2", json.RootElement.GetProperty("observationId").GetString());
        Assert.Equal("tool_correct", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("value").GetDouble());
        Assert.Equal("BOOLEAN", json.RootElement.GetProperty("dataType").GetString());
    }

    [Fact]
    public async Task RecordSessionScoreAsync_PostsSessionIdWithoutTraceId()
    {
        var captured = new List<CapturedRequest>();
        var client = CreateClient(captured);

        await client.RecordSessionScoreAsync(
            "session-9",
            "resolved",
            0.8,
            options: null,
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(Assert.Single(captured).Body!);
        Assert.Equal("session-9", json.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal("resolved", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(0.8, json.RootElement.GetProperty("value").GetDouble());
        Assert.Equal("NUMERIC", json.RootElement.GetProperty("dataType").GetString());
        Assert.False(json.RootElement.TryGetProperty("traceId", out _));
    }
}
