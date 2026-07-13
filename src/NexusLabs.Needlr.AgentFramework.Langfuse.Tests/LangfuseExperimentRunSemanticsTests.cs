using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseExperimentRunSemanticsTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunItemAsync_FirstSuccessfulLinkResolvesRunIdentityAndStructuredLink()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var run = CreateRun(httpClient);

        var result = await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult("done"),
            cancellationToken: _cancellationToken);

        Assert.Equal("done", result.Value);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Linked, result.Link.Status);
        Assert.Equal("dataset-run-item-1", result.Link.DatasetRunItemId);
        Assert.Equal("dataset-run-1", result.Link.DatasetRunId);
        Assert.Null(result.Link.Failure);
        Assert.Equal("dataset-run-1", run.DatasetRunId);
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Resolved, run.IdentityStatus);

        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Complete, snapshot.ApiPublicationStatus);
        Assert.Equal(1, snapshot.ItemLinks.Linked);
        Assert.Equal(0, snapshot.OperationsInFlight);
    }

    [Fact]
    public async Task RunItemAsync_SubsequentLinksPreserveSameRunIdentity()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var run = CreateRun(httpClient);

        var first = await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult(1),
            cancellationToken: _cancellationToken);
        var second = await run.RunItemAsync(
            "case-2",
            (_, _) => Task.FromResult(2),
            cancellationToken: _cancellationToken);

        Assert.Equal("dataset-run-1", first.Link.DatasetRunId);
        Assert.Equal("dataset-run-1", second.Link.DatasetRunId);
        Assert.Equal("dataset-run-1", run.DatasetRunId);
        Assert.Equal(2, run.GetPublicationSnapshot().ItemLinks.Linked);
        Assert.Equal(2, captured.Count);
    }

    [Fact]
    public async Task RunItemAsync_ParallelSameIdentityLinksRemainConsistent()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var run = CreateRun(httpClient);

        var tasks = Enumerable.Range(1, 8)
            .Select(index => run.RunItemAsync(
                $"case-{index}",
                (_, _) => Task.FromResult(index),
                cancellationToken: _cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(8, results.Length);
        Assert.All(results, result => Assert.Equal(LangfuseExperimentItemLinkStatus.Linked, result.Link.Status));
        Assert.All(results, result => Assert.Equal("dataset-run-1", result.Link.DatasetRunId));
        Assert.Equal("dataset-run-1", run.DatasetRunId);
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Resolved, run.IdentityStatus);
        Assert.Equal(8, run.GetPublicationSnapshot().ItemLinks.Linked);
    }

    [Fact]
    public async Task RunItemAsync_ParallelDifferentIdentitiesTransitionRunToInconsistent()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create(
            call => call % 2 == 0 ? "dataset-run-2" : "dataset-run-1",
            captured);
        var run = CreateRun(httpClient);

        var firstTask = run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult(1),
            cancellationToken: _cancellationToken);
        var secondTask = run.RunItemAsync(
            "case-2",
            (_, _) => Task.FromResult(2),
            cancellationToken: _cancellationToken);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(2, results.Length);
        Assert.Equal(1, results.Count(result => result.Link.Status == LangfuseExperimentItemLinkStatus.Linked));
        Assert.Equal(1, results.Count(result => result.Link.Status == LangfuseExperimentItemLinkStatus.Inconsistent));
        Assert.Null(run.DatasetRunId);
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Inconsistent, run.IdentityStatus);

        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(1, snapshot.ItemLinks.Linked);
        Assert.Equal(1, snapshot.ItemLinks.Inconsistent);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Partial, snapshot.ApiPublicationStatus);
    }

    [Fact]
    public async Task RunItemAsync_StrictInconsistentIdentitySkipsCallback()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create(
            call => call == 1 ? "dataset-run-1" : "dataset-run-2",
            captured);
        var run = CreateRun(httpClient);

        await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult("first"),
            cancellationToken: _cancellationToken);
        var callbackInvoked = false;

        await Assert.ThrowsAsync<LangfuseException>(() =>
            run.RunItemAsync(
                "case-2",
                (_, _) =>
                {
                    callbackInvoked = true;
                    return Task.FromResult("second");
                },
                new LangfuseExperimentItemOptions
                {
                    LinkFailureMode = LangfuseExperimentItemLinkFailureMode.Strict,
                },
                _cancellationToken));

        Assert.False(callbackInvoked, "Expected strict identity inconsistency to stop before callback execution.");
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Inconsistent, run.IdentityStatus);
        Assert.Equal(1, run.GetPublicationSnapshot().ItemLinks.Inconsistent);
    }

    [Fact]
    public async Task RunItemAsync_InvalidResponseReturnsStructuredFailureInBestEffortMode()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, """{"id":"","datasetRunId":""}"""),
            []);
        var run = CreateRun(httpClient);
        var callbackInvoked = false;

        var result = await run.RunItemAsync(
            "case-1",
            (_, _) =>
            {
                callbackInvoked = true;
                return Task.FromResult("continued");
            },
            cancellationToken: _cancellationToken);

        Assert.True(callbackInvoked, "Expected best-effort invalid response to continue into the callback.");
        Assert.Equal(LangfuseExperimentItemLinkStatus.Failed, result.Link.Status);
        Assert.NotNull(result.Link.Failure);
        Assert.Equal(LangfusePublicationFailureCode.InvalidResponse, result.Link.Failure!.Code);
        Assert.Null(run.DatasetRunId);
        Assert.Equal(1, run.GetPublicationSnapshot().ItemLinks.Failed);
    }

    [Fact]
    public async Task RunItemAsync_FrozenMetadataIsSubmittedOnEveryLink()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var metadata = new Dictionary<string, object?>
        {
            ["model"] = "candidate-a",
            ["parameters"] = new { temperature = 0.2 },
        };
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var run = CreateRun(
            httpClient,
            new LangfuseExperimentRunOptions
            {
                Description = "comparison run",
                Metadata = metadata,
            });
        metadata["model"] = "mutated";
        metadata["new"] = true;

        await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult(1),
            cancellationToken: _cancellationToken);
        await run.RunItemAsync(
            "case-2",
            (_, _) => Task.FromResult(2),
            cancellationToken: _cancellationToken);

        Assert.Equal("comparison run", run.Description);
        Assert.True(run.Metadata.HasValue, "Expected frozen run metadata.");
        Assert.Equal("candidate-a", run.Metadata.Value.GetProperty("model").GetString());
        Assert.False(run.Metadata.Value.TryGetProperty("new", out _));
        Assert.Equal(2, captured.Count);

        foreach (var request in captured)
        {
            using var json = JsonDocument.Parse(request.Body!);
            Assert.Equal("comparison run", json.RootElement.GetProperty("runDescription").GetString());
            var sentMetadata = json.RootElement.GetProperty("metadata");
            Assert.Equal("candidate-a", sentMetadata.GetProperty("model").GetString());
            Assert.Equal(0.2, sentMetadata.GetProperty("parameters").GetProperty("temperature").GetDouble());
            Assert.False(sentMetadata.TryGetProperty("new", out _));
        }
    }

    [Fact]
    public async Task GetPublicationSnapshot_ReportsInFlightThenCompletedLink()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            requestStarted.SetResult();
            await releaseResponse.Task.WaitAsync(cancellationToken);
            return LangfuseDatasetRunItemHttpStub.CreateResponse(
                "dataset-run-item-1",
                "dataset-run-1",
                json.RootElement.GetProperty("runName").GetString()!,
                json.RootElement.GetProperty("datasetItemId").GetString()!,
                json.RootElement.GetProperty("traceId").GetString()!);
        }));
        var run = CreateRun(httpClient);

        var itemTask = run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult("done"),
            cancellationToken: _cancellationToken);
        await requestStarted.Task.WaitAsync(_cancellationToken);

        var inFlight = run.GetPublicationSnapshot();
        Assert.Equal(1, inFlight.OperationsInFlight);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.InProgress, inFlight.ApiPublicationStatus);

        releaseResponse.SetResult();
        await itemTask;

        var completed = run.GetPublicationSnapshot();
        Assert.Equal(0, completed.OperationsInFlight);
        Assert.Equal(1, completed.ItemLinks.Linked);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Complete, completed.ApiPublicationStatus);
    }

    [Fact]
    public async Task RunItemAsync_CallbackFailureDoesNotEraseSuccessfulLinkPublication()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", []);
        var run = CreateRun(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            run.RunItemAsync<string>(
                "case-1",
                (_, _) => throw new InvalidOperationException("subject failed"),
                cancellationToken: _cancellationToken));

        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(1, snapshot.ItemLinks.Linked);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Complete, snapshot.ApiPublicationStatus);
    }

    [Fact]
    public async Task DisabledRunReturnsStructuredNoOpIdentityAndPublication()
    {
        var run = new DisabledLangfuseExperimentRun(
            "evals",
            "run-abc",
            new LangfuseExperimentRunOptions
            {
                Description = "disabled run",
                Metadata = new { mode = "offline" },
            });

        var item = await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult("done"),
            cancellationToken: _cancellationToken);
        var score = await run.RecordScoreAsync(
            "quality",
            0.9,
            cancellationToken: _cancellationToken);

        Assert.Equal(LangfuseExperimentItemLinkStatus.Disabled, item.Link.Status);
        Assert.Equal(LangfuseExperimentRunScoreStatus.Disabled, score.Status);
        Assert.Null(run.DatasetRunId);
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Disabled, run.IdentityStatus);
        Assert.Equal("disabled run", run.Description);
        Assert.True(run.Metadata.HasValue, "Expected disabled mode to preserve requested metadata.");
        Assert.Equal("offline", run.Metadata.Value.GetProperty("mode").GetString());

        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Disabled, snapshot.ApiPublicationStatus);
        Assert.Equal(1, snapshot.ItemLinks.Disabled);
        Assert.Equal(1, snapshot.RunScores.Disabled);
    }

    private static LangfuseExperimentRun CreateRun(
        HttpClient linkHttpClient,
        LangfuseExperimentRunOptions? options = null,
        LangfuseScoreFailureMode scoreFailureMode = LangfuseScoreFailureMode.Strict,
        HttpClient? scoreHttpClient = null)
    {
        scoreHttpClient ??= LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), []);
        var scoreApiClient = new LangfuseScoreApiClient(
            scoreHttpClient,
            new Uri("https://lf.example/api/public/scores"),
            "Basic x");
        var scoreSink = new LangfuseScoreFailureSink(scoreFailureMode, null);
        var recorder = new LangfuseScoreRecorder(scoreApiClient, scoreSink, normalizeNames: false);
        return new LangfuseExperimentRun(
            new LangfuseApiClient(linkHttpClient, BaseUrl, "Basic x"),
            new LangfuseScoreClient(recorder, scoreSink),
            recorder,
            datasetName: "evals",
            runName: "run-abc",
            options,
            diagnostics: null);
    }

}
