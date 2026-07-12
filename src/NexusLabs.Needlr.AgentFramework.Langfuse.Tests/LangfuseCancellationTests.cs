using System.Diagnostics;
using System.Net;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseCancellationTests
{
    private readonly CancellationToken _testCancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApiClientPostAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = CreateCanceledTokenSource();
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new LangfuseApiClient(httpClient, new Uri("https://lf.example/"), "Basic x");

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.PostAsync("api/public/datasets", new { name = "evals" }, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task ScoreApiClientCreateAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = CreateCanceledTokenSource();
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://lf.example/api/public/scores"),
            "Basic x");

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CreateAsync(CreateScore(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task ScenarioRecordScoreAsync_NonFatalMode_WhenCallerCancels_DoesNotRecordFailure()
    {
        using var cancellation = CreateCanceledTokenSource();
        using var listener = LangfuseTestFactory.StartListener();
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        LangfuseScoreError? capturedError = null;
        var failureSink = new LangfuseScoreFailureSink(
            LangfuseScoreFailureMode.NonFatal,
            error => capturedError = error);
        var recorder = new LangfuseScoreRecorder(
            new LangfuseScoreApiClient(
                httpClient,
                new Uri("https://lf.example/api/public/scores"),
                "Basic x"),
            failureSink,
            normalizeNames: false);

        using var scenario = new LangfuseScenario(recorder, "scenario", null, null, null, null);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scenario.RecordScoreAsync("correctness", 1.0, cancellationToken: cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, failureSink.FailedCount);
        Assert.Null(capturedError);
    }

    [Fact]
    public async Task CommentRecorder_WhenCallerCancels_DoesNotReportDiagnostic()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_testCancellationToken);
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = CreateHttpClient(
            (_, token) =>
            {
                requestStarted.SetResult();
                return pendingResponse.Task.WaitAsync(token);
            });
        var apiClient = new LangfuseApiClient(httpClient, new Uri("https://lf.example/"), "Basic x");

        string? diagnostic = null;
        var recorder = new LangfuseCommentRecorder(apiClient, message => diagnostic = message);

        var commentTask = recorder.AddTraceCommentAsync("trace-1", "comment", cancellation.Token);
        await requestStarted.Task.WaitAsync(_testCancellationToken);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            commentTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Null(diagnostic);
    }

    [Fact]
    public async Task ExperimentRunBeginItemAsync_WhenCallerCancels_StopsScenarioWithoutReportingDiagnostic()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_testCancellationToken);
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stoppedActivities = 0;
        using var listener = StartListener(_ => Interlocked.Increment(ref stoppedActivities));
        using var httpClient = CreateHttpClient(
            (_, token) =>
            {
                requestStarted.SetResult();
                return pendingResponse.Task.WaitAsync(token);
            });
        var apiClient = new LangfuseApiClient(httpClient, new Uri("https://lf.example/"), "Basic x");

        string? diagnostic = null;
        var run = new LangfuseExperimentRun(
            apiClient,
            LangfuseTestFactory.OkScoreRecorder(),
            datasetName: "evals",
            runName: "run-1",
            runDescription: null,
            diagnostics: message => diagnostic = message);

        var beginTask = run.BeginItemAsync("case-1", cancellationToken: cancellation.Token);
        await requestStarted.Task.WaitAsync(_testCancellationToken);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            beginTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, stoppedActivities);
        Assert.Null(diagnostic);
    }

    [Fact]
    public async Task ApiClientPostAsync_WhenTransportTimesOut_WrapsLangfuseException()
    {
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("simulated timeout")));
        var client = new LangfuseApiClient(httpClient, new Uri("https://lf.example/"), "Basic x");

        var exception = await Assert.ThrowsAsync<LangfuseException>(() =>
            client.PostAsync("api/public/datasets", new { name = "evals" }, _testCancellationToken));

        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task ScoreApiClientCreateAsync_WhenTransportTimesOut_WrapsLangfuseException()
    {
        using var httpClient = CreateHttpClient(
            (_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("simulated timeout")));
        var client = new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://lf.example/api/public/scores"),
            "Basic x");

        var exception = await Assert.ThrowsAsync<LangfuseException>(() =>
            client.CreateAsync(CreateScore(), _testCancellationToken));

        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    private CancellationTokenSource CreateCanceledTokenSource()
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_testCancellationToken);
        cancellation.Cancel();
        return cancellation;
    }

    private static LangfuseScore CreateScore() =>
        new()
        {
            TraceId = "trace-1",
            Name = "correctness",
            Value = 1.0,
            DataType = "NUMERIC",
        };

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) =>
        new(new DelegateHttpMessageHandler(sendAsync));

    private static ActivityListener StartListener(Action<Activity> onStopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == LangfuseActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = onStopped,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
