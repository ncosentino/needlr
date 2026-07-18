using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfusePublicationHealthTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScoreUploads_ReportSuccessAndFailureIndependently()
    {
        var call = 0;
        using var httpClient = LangfuseHttpStub.Create(
            _ => Interlocked.Increment(ref call) == 1
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, """{"id":"score-1"}""")
                : LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "rejected"),
            []);
        var health = new LangfusePublicationHealth(isEnabled: true);
        var apiClient = new LangfuseApiClient(
            httpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            health: health);
        var failureSink = new LangfuseScoreFailureSink(
            LangfuseScoreFailureMode.NonFatal,
            callback: null);
        var recorder = new LangfuseScoreRecorder(
            new LangfuseScoreApiClient(apiClient),
            failureSink,
            normalizeNames: false,
            health: health);

        await recorder.RecordNumericAsync(
            "trace-1",
            "accepted",
            1,
            new LangfuseScoreOptions { Id = "score-1" },
            _cancellationToken);
        await recorder.RecordNumericAsync(
            "trace-1",
            "rejected",
            0,
            options: null,
            _cancellationToken);

        var scores = health.GetSnapshot().ScoreUploads;
        Assert.Equal(0, scores.InFlight);
        Assert.Equal(1, scores.Succeeded);
        Assert.Equal(1, scores.Failed);
        Assert.Equal(0, scores.Canceled);
    }

    [Fact]
    public async Task ItemLinkFailure_IsSeparateFromCallbackResult()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var linkHttpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "missing item"),
            []);
        using var scoreHttpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, """{"id":"score-1"}"""),
            []);
        var health = new LangfusePublicationHealth(isEnabled: true);
        var linkApiClient = new LangfuseApiClient(
            linkHttpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            health: health);
        var failureSink = new LangfuseScoreFailureSink(
            LangfuseScoreFailureMode.NonFatal,
            callback: null);
        var recorder = new LangfuseScoreRecorder(
            LangfuseTestFactory.CreateScoreApiClient(scoreHttpClient),
            failureSink,
            normalizeNames: false,
            health: health);
        var run = new LangfuseExperimentRun(
            linkApiClient,
            new LangfuseScoreClient(recorder, failureSink),
            recorder,
            datasetName: "evals",
            runName: "run-1",
            options: null,
            diagnostics: null,
            health: health);

        var result = await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult("subject-result"),
            options: null,
            cancellationToken: _cancellationToken);

        Assert.Equal("subject-result", result.Value);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Failed, result.Link.Status);
        var links = health.GetSnapshot().ItemLinks;
        Assert.Equal(0, links.InFlight);
        Assert.Equal(0, links.Succeeded);
        Assert.Equal(1, links.Failed);
        Assert.Equal(0, links.Canceled);
    }

    [Fact]
    public async Task ParallelScorePublication_ReportsEveryAcceptedRequestWithoutCounterRaces()
    {
        var handler = new TrackingHttpMessageHandler(request =>
        {
            using var json = JsonDocument.Parse(request.Body!);
            var id = json.RootElement.GetProperty("id").GetString();
            return LangfuseHttpStub.Json(
                HttpStatusCode.OK,
                $$"""{"id":"{{id}}"}""");
        });
        using var httpClient = new HttpClient(handler);
        var health = new LangfusePublicationHealth(isEnabled: true);
        var apiClient = new LangfuseApiClient(
            httpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            health: health);
        var recorder = new LangfuseScoreRecorder(
            new LangfuseScoreApiClient(apiClient),
            new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null),
            normalizeNames: false,
            health: health);

        await Task.WhenAll(Enumerable.Range(0, 100).Select(index =>
            recorder.RecordNumericAsync(
                $"trace-{index}",
                "quality",
                index,
                new LangfuseScoreOptions { Id = $"score-{index}" },
                _cancellationToken)));

        var scores = health.GetSnapshot().ScoreUploads;
        Assert.Equal(0, scores.InFlight);
        Assert.Equal(100, scores.Succeeded);
        Assert.Equal(0, scores.Failed);
        Assert.Equal(100, handler.CapturedRequests.Count);
    }

    [Fact]
    public async Task CanceledScoreUpload_ClearsInFlightAndPreservesCallerCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(
            (_, token) =>
            {
                requestStarted.SetResult();
                return pendingResponse.Task.WaitAsync(token);
            }));
        var health = new LangfusePublicationHealth(isEnabled: true);
        var apiClient = new LangfuseApiClient(
            httpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            health: health);
        var recorder = new LangfuseScoreRecorder(
            new LangfuseScoreApiClient(apiClient),
            new LangfuseScoreFailureSink(LangfuseScoreFailureMode.NonFatal, null),
            normalizeNames: false,
            health: health);

        var recordTask = recorder.RecordNumericAsync(
            "trace-1",
            "quality",
            1,
            options: null,
            cancellation.Token);
        await requestStarted.Task.WaitAsync(_cancellationToken);
        Assert.Equal(1, health.GetSnapshot().ScoreUploads.InFlight);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => recordTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        var scores = health.GetSnapshot().ScoreUploads;
        Assert.Equal(0, scores.InFlight);
        Assert.Equal(0, scores.Succeeded);
        Assert.Equal(0, scores.Failed);
        Assert.Equal(1, scores.Canceled);
    }
}
