using System.Net;
using System.Text.Json;

using Moq;
using Moq.Protected;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseScoreRecorderTests
{
    [Fact]
    public async Task NormalizeScoreNames_ConvertsToSnakeCase()
    {
        var recorder = CreateRecorder(out var bodies, normalizeNames: true);

        await recorder.RecordNumericAsync("trace-1", "All Tool Calls Succeeded", 1.0, null, TestContext.Current.CancellationToken);

        var body = Assert.Single(bodies);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("all_tool_calls_succeeded", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task WithoutNormalization_SendsNameVerbatim()
    {
        var recorder = CreateRecorder(out var bodies, normalizeNames: false);

        await recorder.RecordNumericAsync("trace-1", "All Tool Calls Succeeded", 1.0, null, TestContext.Current.CancellationToken);

        var body = Assert.Single(bodies);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("All Tool Calls Succeeded", json.RootElement.GetProperty("name").GetString());
    }

    private static LangfuseScoreRecorder CreateRecorder(out List<string> capturedBodies, bool normalizeNames)
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

        var apiClient = new LangfuseScoreApiClient(
            new HttpClient(handler.Object),
            new Uri("https://cloud.langfuse.com/api/public/scores"),
            "Basic cGs6c2s=");
        var sink = new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null);
        return new LangfuseScoreRecorder(apiClient, sink, normalizeNames);
    }
}
