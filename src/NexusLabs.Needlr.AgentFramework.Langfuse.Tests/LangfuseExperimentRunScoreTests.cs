using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseExperimentRunScoreTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordScoreAsync_PostsNumericBooleanAndCategoricalDatasetRunScores()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var scoreRequests = new List<CapturedRequest>();
        var run = await CreateResolvedRunAsync(scoreRequests);

        var numeric = await run.RecordScoreAsync(
            "average_accuracy",
            0.92,
            new LangfuseScoreOptions
            {
                Id = "run-score-average-accuracy",
                Comment = "aggregate",
            },
            _cancellationToken);
        var boolean = await run.RecordScoreAsync(
            "passed",
            true,
            cancellationToken: _cancellationToken);
        var categorical = await run.RecordScoreAsync(
            "verdict",
            "acceptable",
            cancellationToken: _cancellationToken);

        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, numeric.Status);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, boolean.Status);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, categorical.Status);
        Assert.Equal("dataset-run-1", numeric.DatasetRunId);
        Assert.Equal("run-score-average-accuracy", numeric.ScoreId);
        Assert.Equal(3, scoreRequests.Count);

        using var numericJson = JsonDocument.Parse(scoreRequests[0].Body!);
        Assert.Equal("dataset-run-1", numericJson.RootElement.GetProperty("datasetRunId").GetString());
        Assert.Equal("average_accuracy", numericJson.RootElement.GetProperty("name").GetString());
        Assert.Equal(0.92, numericJson.RootElement.GetProperty("value").GetDouble());
        Assert.Equal("NUMERIC", numericJson.RootElement.GetProperty("dataType").GetString());
        Assert.Equal("run-score-average-accuracy", numericJson.RootElement.GetProperty("id").GetString());
        Assert.False(numericJson.RootElement.TryGetProperty("traceId", out _));

        using var booleanJson = JsonDocument.Parse(scoreRequests[1].Body!);
        Assert.Equal(1, booleanJson.RootElement.GetProperty("value").GetDouble());
        Assert.Equal("BOOLEAN", booleanJson.RootElement.GetProperty("dataType").GetString());

        using var categoricalJson = JsonDocument.Parse(scoreRequests[2].Body!);
        Assert.Equal("acceptable", categoricalJson.RootElement.GetProperty("value").GetString());
        Assert.Equal("CATEGORICAL", categoricalJson.RootElement.GetProperty("dataType").GetString());

        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(3, snapshot.RunScores.Accepted);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Complete, snapshot.ApiPublicationStatus);
    }

    [Fact]
    public async Task RecordEvaluationAsync_ProjectsMetricsAndReturnsSkippedForUnsetValues()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var scoreRequests = new List<CapturedRequest>();
        var run = await CreateResolvedRunAsync(scoreRequests);
        var result = new EvaluationResult(
            new NumericMetric("accuracy", 0.8),
            new BooleanMetric("passed", true),
            new StringMetric("verdict", "good"),
            new NumericMetric("unset"));

        var outcomes = await run.RecordEvaluationAsync(
            result,
            new LangfuseEvaluationScoreOptions
            {
                ScoreIdProvider = metric => $"run-score:{metric.Name}",
            },
            cancellationToken: _cancellationToken);

        Assert.Equal(4, outcomes.Count);
        Assert.Equal(
            [LangfuseExperimentScoreStatus.Accepted, LangfuseExperimentScoreStatus.Accepted, LangfuseExperimentScoreStatus.Accepted, LangfuseExperimentScoreStatus.Skipped],
            outcomes.Select(outcome => outcome.Status).ToArray());
        Assert.Equal(["accuracy", "passed", "verdict", "unset"], outcomes.Select(outcome => outcome.Name).ToArray());
        Assert.Equal(
            new string?[]
            {
                "run-score:accuracy",
                "run-score:passed",
                "run-score:verdict",
                "run-score:unset",
            },
            outcomes.Select(outcome => outcome.ScoreId).ToArray());
        Assert.Equal(3, scoreRequests.Count);

        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(3, snapshot.RunScores.Accepted);
        Assert.Equal(1, snapshot.RunScores.Skipped);
    }

    [Fact]
    public async Task RecordScoreAsync_WithoutResolvedIdentityReturnsNotAttemptedInNonFatalMode()
    {
        using var linkHttpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), []);
        LangfuseScoreError? capturedError = null;
        var run = CreateRun(
            linkHttpClient,
            LangfuseScoreFailureMode.NonFatal,
            scoreHttpClient: LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, []),
            scoreErrorCallback: error => capturedError = error);

        var result = await run.RecordScoreAsync(
            "quality",
            0.9,
            cancellationToken: _cancellationToken);

        Assert.Equal(LangfuseExperimentScoreStatus.NotAttempted, result.Status);
        Assert.Null(result.DatasetRunId);
        Assert.NotNull(result.Failure);
        Assert.Equal(LangfusePublicationFailureCode.DatasetRunIdentityUnavailable, result.Failure!.Code);
        Assert.NotNull(capturedError);

        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(1, snapshot.RunScores.NotAttempted);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Failed, snapshot.ApiPublicationStatus);
    }

    [Fact]
    public async Task RecordScoreAsync_WithoutResolvedIdentityThrowsInStrictModeAndRecordsSnapshot()
    {
        using var linkHttpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), []);
        var run = CreateRun(
            linkHttpClient,
            LangfuseScoreFailureMode.Strict,
            scoreHttpClient: LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, []));

        await Assert.ThrowsAsync<LangfuseException>(() =>
            run.RecordScoreAsync(
                "quality",
                0.9,
                cancellationToken: _cancellationToken));

        Assert.Equal(1, run.GetPublicationSnapshot().RunScores.NotAttempted);
    }

    [Fact]
    public async Task RecordScoreAsync_ApiRejectionReturnsFailedInNonFatalMode()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var scoreHttpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "score rejected"),
            []);
        var run = await CreateResolvedRunAsync(
            [],
            LangfuseScoreFailureMode.NonFatal,
            scoreHttpClient);

        var result = await run.RecordScoreAsync(
            "quality",
            0.9,
            cancellationToken: _cancellationToken);

        Assert.Equal(LangfuseExperimentScoreStatus.Failed, result.Status);
        Assert.NotNull(result.Failure);
        Assert.Equal(LangfusePublicationFailureCode.ApiRejected, result.Failure!.Code);
        Assert.Equal(1, run.GetPublicationSnapshot().RunScores.Failed);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Partial, run.GetPublicationSnapshot().ApiPublicationStatus);
    }

    [Fact]
    public async Task RecordScoreAsync_ApiRejectionThrowsInStrictModeAndRecordsSnapshot()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var scoreHttpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "score rejected"),
            []);
        var run = await CreateResolvedRunAsync(
            [],
            LangfuseScoreFailureMode.Strict,
            scoreHttpClient);

        await Assert.ThrowsAnyAsync<LangfuseException>(() =>
            run.RecordScoreAsync(
                "quality",
                0.9,
                cancellationToken: _cancellationToken));

        Assert.Equal(1, run.GetPublicationSnapshot().RunScores.Failed);
    }

    [Fact]
    public async Task RecordScoreAsync_AfterInconsistentIdentityReturnsNotAttempted()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var linkRequests = new List<CapturedRequest>();
        using var linkHttpClient = LangfuseDatasetRunItemHttpStub.Create(
            call => call == 1 ? "dataset-run-1" : "dataset-run-2",
            linkRequests);
        var run = CreateRun(
            linkHttpClient,
            LangfuseScoreFailureMode.NonFatal,
            LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, []));
        await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult(1),
            cancellationToken: _cancellationToken);
        await run.RunItemAsync(
            "case-2",
            (_, _) => Task.FromResult(2),
            cancellationToken: _cancellationToken);

        var result = await run.RecordScoreAsync(
            "quality",
            0.9,
            cancellationToken: _cancellationToken);

        Assert.Equal(LangfuseExperimentScoreStatus.NotAttempted, result.Status);
        Assert.NotNull(result.Failure);
        Assert.Equal(
            LangfusePublicationFailureCode.InconsistentDatasetRunIdentity,
            result.Failure!.Code);
        Assert.Null(run.DatasetRunId);
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Inconsistent, run.IdentityStatus);
    }

    [Fact]
    public async Task RecordScoreAsync_WhenCallerCancelsRequest_PreservesTokenAndClearsInFlight()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var scoreHttpClient = new HttpClient(new DelegateHttpMessageHandler(
            (_, token) =>
            {
                requestStarted.SetResult();
                return pendingResponse.Task.WaitAsync(token);
            }));
        var run = await CreateResolvedRunAsync(
            [],
            LangfuseScoreFailureMode.NonFatal,
            scoreHttpClient);

        var scoreTask = run.RecordScoreAsync(
            "quality",
            0.9,
            cancellationToken: cancellation.Token);
        await requestStarted.Task.WaitAsync(_cancellationToken);
        Assert.Equal(1, run.GetPublicationSnapshot().OperationsInFlight);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scoreTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(0, snapshot.OperationsInFlight);
        Assert.Equal(0, snapshot.RunScores.Total);
    }

    [Fact]
    public async Task RecordScoreAsync_PreCanceledTokenDoesNotChangePublicationSnapshot()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        cancellation.Cancel();
        using var linkHttpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), []);
        var run = CreateRun(
            linkHttpClient,
            LangfuseScoreFailureMode.NonFatal,
            scoreHttpClient: LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, []));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            run.RecordScoreAsync(
                "quality",
                0.9,
                cancellationToken: cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(0, snapshot.RunScores.Total);
        Assert.Equal(LangfuseExperimentApiPublicationStatus.NotAttempted, snapshot.ApiPublicationStatus);
    }

    [Fact]
    public async Task RecordEvaluationAsync_CancellationBetweenUnavailableMetricsDoesNotCountNextMetric()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        using var linkHttpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), []);
        var run = CreateRun(
            linkHttpClient,
            LangfuseScoreFailureMode.NonFatal,
            LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, []),
            scoreErrorCallback: _ => cancellation.Cancel());
        var evaluation = new EvaluationResult(
            new NumericMetric("first", 1),
            new NumericMetric("second", 2));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            run.RecordEvaluationAsync(
                evaluation,
                cancellationToken: cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, run.GetPublicationSnapshot().RunScores.NotAttempted);
    }

    [Fact]
    public async Task RecordEvaluationAsync_WithoutIdentitySkipsUnsetMetricWithoutFailure()
    {
        using var linkHttpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), []);
        var failureCount = 0;
        var run = CreateRun(
            linkHttpClient,
            LangfuseScoreFailureMode.NonFatal,
            LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, []),
            scoreErrorCallback: _ => Interlocked.Increment(ref failureCount));
        var evaluation = new EvaluationResult(
            new NumericMetric("publishable", 1),
            new NumericMetric("unset"));

        var results = await run.RecordEvaluationAsync(
            evaluation,
            cancellationToken: _cancellationToken);

        Assert.Equal(
            [LangfuseExperimentScoreStatus.NotAttempted, LangfuseExperimentScoreStatus.Skipped],
            results.Select(result => result.Status).ToArray());
        Assert.Equal(1, failureCount);
        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(1, snapshot.RunScores.NotAttempted);
        Assert.Equal(1, snapshot.RunScores.Skipped);
    }

    private async Task<LangfuseExperimentRun> CreateResolvedRunAsync(
        List<CapturedRequest> scoreRequests,
        LangfuseScoreFailureMode failureMode = LangfuseScoreFailureMode.Strict,
        HttpClient? scoreHttpClient = null)
    {
        var linkRequests = new List<CapturedRequest>();
        var linkHttpClient = LangfuseDatasetRunItemHttpStub.Create(
            "dataset-run-1",
            linkRequests);
        scoreHttpClient ??= LangfuseHttpStub.Create(
            LangfuseHttpStub.ScoreAccepted,
            scoreRequests);
        var run = CreateRun(linkHttpClient, failureMode, scoreHttpClient);

        await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult("done"),
            cancellationToken: _cancellationToken);
        return run;
    }

    private static LangfuseExperimentRun CreateRun(
        HttpClient linkHttpClient,
        LangfuseScoreFailureMode failureMode,
        HttpClient scoreHttpClient,
        Action<LangfuseScoreError>? scoreErrorCallback = null)
    {
        var scoreApiClient = LangfuseTestFactory.CreateScoreApiClient(scoreHttpClient);
        var scoreSink = new LangfuseScoreFailureSink(failureMode, scoreErrorCallback);
        var recorder = new LangfuseScoreRecorder(scoreApiClient, scoreSink, normalizeNames: false);
        return new LangfuseExperimentRun(
            new LangfuseApiClient(linkHttpClient, new Uri("https://lf.example/"), "Basic x"),
            new LangfuseScoreClient(recorder, scoreSink),
            recorder,
            datasetName: "evals",
            runName: "run-abc",
            options: null,
            diagnostics: null);
    }
}
