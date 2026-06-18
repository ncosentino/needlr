using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseExperimentRunTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");

    [Fact]
    public async Task BeginItemAsync_LinksScenarioTraceToRunItem()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), captured);
        var apiClient = new LangfuseApiClient(httpClient, BaseUrl, "Basic x");
        var run = new LangfuseExperimentRun(
            apiClient,
            LangfuseTestFactory.OkScoreRecorder(),
            datasetName: "evals",
            runName: "run-abc",
            runDescription: null,
            diagnostics: null);

        using var scenario = await run.BeginItemAsync("case-1", cancellationToken: TestContext.Current.CancellationToken);

        var post = Assert.Single(captured, c => c.Uri.AbsolutePath.EndsWith("/dataset-run-items", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("run-abc", json.RootElement.GetProperty("runName").GetString());
        Assert.Equal("case-1", json.RootElement.GetProperty("datasetItemId").GetString());
        Assert.Equal(scenario.TraceId, json.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task BeginItemAsync_WhenLinkFails_IsNonFatalAndReportsDiagnostic()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "no such dataset item"),
            []);
        var apiClient = new LangfuseApiClient(httpClient, BaseUrl, "Basic x");

        string? diagnostic = null;
        var run = new LangfuseExperimentRun(
            apiClient,
            LangfuseTestFactory.OkScoreRecorder(),
            datasetName: "evals",
            runName: "run-abc",
            runDescription: null,
            diagnostics: d => diagnostic = d);

        using var scenario = await run.BeginItemAsync("missing", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(scenario.TraceId);
        Assert.NotNull(diagnostic);
        Assert.Contains("missing", diagnostic!, StringComparison.Ordinal);
    }
}
