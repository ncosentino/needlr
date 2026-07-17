using System.Diagnostics;
using System.Net;
using System.Text.Json;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseExperimentRunTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunItemAsync_LinksScenarioTraceAndReturnsCallbackValue()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create(
            "dataset-run-1",
            captured);
        var apiClient = new LangfuseApiClient(httpClient, BaseUrl, "Basic x");
        var run = new LangfuseExperimentRun(
            apiClient,
            LangfuseTestFactory.OkScoreRecorder(),
            datasetName: "evals",
            runName: "run-abc",
            options: null,
            diagnostics: null);

        var result = await run.RunItemAsync(
            "case-1",
            (scenario, cancellationToken) =>
            {
                Assert.Same(scenario.Activity, Activity.Current);
                Assert.Equal("custom scenario", scenario.Activity!.GetTagItem("langfuse.trace.name"));
                Assert.Equal("value", scenario.Activity.GetTagItem("langfuse.trace.metadata.key"));
                Assert.Equal(["experiment"], Assert.IsType<string[]>(scenario.Activity.GetTagItem("langfuse.trace.tags")));
                Assert.Equal(_cancellationToken, cancellationToken);
                return Task.FromResult("callback-result");
            },
            new LangfuseExperimentItemOptions
            {
                ScenarioName = "custom scenario",
                Tags = ["experiment"],
                Metadata = new Dictionary<string, string> { ["key"] = "value" },
            },
            _cancellationToken);

        var post = Assert.Single(captured, c => c.Uri.AbsolutePath.EndsWith("/dataset-run-items", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("run-abc", json.RootElement.GetProperty("runName").GetString());
        Assert.Equal("case-1", json.RootElement.GetProperty("datasetItemId").GetString());
        Assert.Equal(result.TraceId, json.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("callback-result", result.Value);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Linked, result.Link.Status);
    }

    [Fact]
    public async Task RunItemAsync_ChildActivityUsesScenarioAsParent()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", []);
        var run = CreateRun(httpClient);

        var result = await run.RunItemAsync(
            "case-1",
            (scenario, _) =>
            {
                using var child = LangfuseActivitySource.Source.StartActivity("agent.tool")!;
                Assert.Equal(scenario.Activity!.TraceId, child.TraceId);
                Assert.Equal(scenario.Activity.SpanId, child.ParentSpanId);
                return Task.FromResult(child.TraceId.ToString());
            },
            options: null,
            cancellationToken: _cancellationToken);

        Assert.Equal(result.TraceId, result.Value);
    }

    [Fact]
    public async Task RunItemAsync_SuccessRestoresCallerActivityAndStopsScenario()
    {
        var stoppedActivities = 0;
        using var listener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        using var caller = new Activity("caller").Start();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", []);
        var run = CreateRun(httpClient);

        var result = await run.RunItemAsync(
            "case-1",
            (scenario, _) =>
            {
                Assert.Same(scenario.Activity, Activity.Current);
                return Task.FromResult("done");
            },
            options: null,
            cancellationToken: _cancellationToken);

        Assert.Equal("done", result.Value);
        Assert.Same(caller, Activity.Current);
        Assert.Equal(1, stoppedActivities);
    }

    [Fact]
    public async Task RunItemAsync_CallbackFailureRestoresCallerActivityAndStopsScenario()
    {
        var stoppedActivities = 0;
        using var listener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        using var caller = new Activity("caller").Start();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", []);
        var run = CreateRun(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            run.RunItemAsync<string>(
                "case-1",
                async (scenario, _) =>
                {
                    Assert.Same(scenario.Activity, Activity.Current);
                    await Task.Yield();
                    throw new InvalidOperationException("callback failed");
                },
                options: null,
                cancellationToken: _cancellationToken));

        Assert.Equal("callback failed", exception.Message);
        Assert.Same(caller, Activity.Current);
        Assert.Equal(1, stoppedActivities);
    }

    [Fact]
    public async Task RunItemAsync_ParallelItemsUseDistinctIsolatedActivities()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var caller = new Activity("caller").Start();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var run = CreateRun(httpClient);
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<string> ExecuteAsync(ILangfuseScenario scenario, CancellationToken cancellationToken)
        {
            Assert.Same(scenario.Activity, Activity.Current);
            if (Interlocked.Increment(ref entered) == 2)
            {
                bothEntered.SetResult();
            }

            await bothEntered.Task.WaitAsync(cancellationToken);
            Assert.Same(scenario.Activity, Activity.Current);
            return scenario.TraceId!;
        }

        var firstTask = run.RunItemAsync(
            "case-1",
            ExecuteAsync,
            options: null,
            cancellationToken: _cancellationToken);
        var secondTask = run.RunItemAsync(
            "case-2",
            ExecuteAsync,
            options: null,
            cancellationToken: _cancellationToken);
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(2, results.Length);
        Assert.NotEqual(results[0].TraceId, results[1].TraceId);
        Assert.Equal(results[0].TraceId, results[0].Value);
        Assert.Equal(results[1].TraceId, results[1].Value);
        Assert.Same(caller, Activity.Current);
        Assert.Equal(2, captured.Count);
    }

    [Fact]
    public async Task RunItemAsync_NestedItemRestoresOuterActivity()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var run = CreateRun(httpClient);
        string? outerTraceId = null;

        var outerResult = await run.RunItemAsync(
            "outer",
            async (outerScenario, cancellationToken) =>
            {
                outerTraceId = outerScenario.TraceId;
                Assert.Same(outerScenario.Activity, Activity.Current);

                var innerResult = await run.RunItemAsync(
                    "inner",
                    (innerScenario, _) =>
                    {
                        Assert.Same(innerScenario.Activity, Activity.Current);
                        Assert.NotEqual(outerScenario.TraceId, innerScenario.TraceId);
                        return Task.FromResult(innerScenario.TraceId!);
                    },
                    options: null,
                    cancellationToken: cancellationToken);

                Assert.Same(outerScenario.Activity, Activity.Current);
                return innerResult.TraceId;
            },
            options: null,
            cancellationToken: _cancellationToken);

        Assert.Equal(outerTraceId, outerResult.TraceId);
        Assert.NotEqual(outerResult.TraceId, outerResult.Value);
        Assert.Equal(2, captured.Count);
    }

    [Fact]
    public async Task RunItemAsync_BestEffortLinkFailureRunsCallbackAndReturnsFailed()
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
            options: null,
            diagnostics: d => diagnostic = d);

        var callbackInvoked = false;
        var result = await run.RunItemAsync(
            "missing",
            (scenario, _) =>
            {
                callbackInvoked = true;
                Assert.Same(scenario.Activity, Activity.Current);
                return Task.FromResult("continued");
            },
            options: null,
            cancellationToken: _cancellationToken);

        Assert.True(callbackInvoked, "Expected best-effort link failure to continue into the callback.");
        Assert.Equal("continued", result.Value);
        Assert.NotNull(result.TraceId);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Failed, result.Link.Status);
        Assert.NotNull(diagnostic);
        Assert.Contains("missing", diagnostic!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunItemAsync_StrictLinkFailureSkipsCallbackAndStopsScenario()
    {
        var stoppedActivities = 0;
        using var listener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "no such dataset item"),
            []);
        var run = CreateRun(httpClient);
        var callbackInvoked = false;

        await Assert.ThrowsAnyAsync<LangfuseException>(() =>
            run.RunItemAsync(
                "missing",
                (_, _) =>
                {
                    callbackInvoked = true;
                    return Task.FromResult("not reached");
                },
                new LangfuseExperimentItemOptions
                {
                    LinkFailureMode = LangfuseExperimentItemLinkFailureMode.Strict,
                },
                _cancellationToken));

        Assert.False(callbackInvoked, "Expected strict link failure to stop before callback execution.");
        Assert.Equal(1, stoppedActivities);
    }

    [Fact]
    public async Task RunItemAsync_UnsampledScenarioRunsCallbackWithoutPosting()
    {
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var run = CreateRun(httpClient);

        var result = await run.RunItemAsync(
            "case-1",
            (scenario, _) =>
            {
                Assert.Null(scenario.Activity);
                Assert.Same(scenario.Activity, Activity.Current);
                return Task.FromResult("not sampled");
            },
            options: null,
            cancellationToken: _cancellationToken);

        Assert.Equal("not sampled", result.Value);
        Assert.Null(result.TraceId);
        Assert.Equal(LangfuseExperimentItemLinkStatus.NotSampled, result.Link.Status);
        Assert.Empty(handler.CapturedRequests);
    }

    [Fact]
    public async Task RunItemAsync_StrictModeWithoutSampledTraceRunsCallbackAndReturnsNotSampled()
    {
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var run = CreateRun(httpClient);

        var result = await run.RunItemAsync(
            "case-1",
            (_, _) => Task.FromResult("continued"),
            new LangfuseExperimentItemOptions
            {
                LinkFailureMode = LangfuseExperimentItemLinkFailureMode.Strict,
            },
            _cancellationToken);

        Assert.Equal("continued", result.Value);
        Assert.Equal(LangfuseExperimentItemLinkStatus.NotSampled, result.Link.Status);
        Assert.Empty(handler.CapturedRequests);
    }

    [Fact]
    public async Task RunItemAsync_PropagationOnlyActivityReturnsNotSampledWithoutPosting()
    {
        using var provider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOffSampler())
            .AddSource(LangfuseActivitySource.Name)
            .Build();
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var run = CreateRun(httpClient);

        var result = await run.RunItemAsync(
            "case-1",
            (scenario, _) =>
            {
                Assert.NotNull(scenario.Activity);
                Assert.False(scenario.Activity!.Recorded, "Expected an unsampled propagation-only activity.");
                Assert.NotNull(scenario.TraceId);
                return Task.FromResult(scenario.TraceId);
            },
            options: null,
            cancellationToken: _cancellationToken);

        Assert.NotNull(result.Value);
        Assert.Null(result.TraceId);
        Assert.Equal(LangfuseExperimentItemLinkStatus.NotSampled, result.Link.Status);
        Assert.Empty(handler.CapturedRequests);
    }

    [Fact]
    public async Task RunItemAsync_WithUndefinedLinkFailureMode_RejectsBeforeCreatingScenario()
    {
        var startedActivities = 0;
        using var listener = LangfuseTestFactory.StartListener(
            onStarted: _ => Interlocked.Increment(ref startedActivities));
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var run = CreateRun(httpClient);
        var callbackInvoked = false;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            run.RunItemAsync(
                "case-1",
                (_, _) =>
                {
                    callbackInvoked = true;
                    return Task.FromResult("not reached");
                },
                new LangfuseExperimentItemOptions
                {
                    LinkFailureMode = (LangfuseExperimentItemLinkFailureMode)int.MaxValue,
                },
                _cancellationToken));

        Assert.False(callbackInvoked, "Expected invalid options to stop before callback execution.");
        Assert.Equal(0, startedActivities);
        Assert.Empty(handler.CapturedRequests);
    }

    [Fact]
    public async Task RunItemAsync_CallbackLangfuseExceptionIsNotTreatedAsLinkFailure()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", []);
        string? diagnostic = null;
        var run = new LangfuseExperimentRun(
            new LangfuseApiClient(httpClient, BaseUrl, "Basic x"),
            LangfuseTestFactory.OkScoreRecorder(),
            datasetName: "evals",
            runName: "run-abc",
            options: null,
            diagnostics: message => diagnostic = message);

        var exception = await Assert.ThrowsAsync<LangfuseException>(() =>
            run.RunItemAsync<string>(
                "case-1",
                (_, _) => throw new LangfuseException("callback failure"),
                options: null,
                cancellationToken: _cancellationToken));

        Assert.Equal("callback failure", exception.Message);
        Assert.Null(diagnostic);
    }

    [Fact]
    public async Task DisabledRunItemAsync_InvokesCallbackAndReturnsDisabled()
    {
        var run = new DisabledLangfuseExperimentRun("evals", "run-abc", options: null);

        var result = await run.RunItemAsync(
            "case-1",
            (scenario, cancellationToken) =>
            {
                Assert.Null(scenario.Activity);
                Assert.Null(scenario.TraceId);
                Assert.Equal(_cancellationToken, cancellationToken);
                return Task.FromResult(42);
            },
            options: null,
            cancellationToken: _cancellationToken);

        Assert.Equal(42, result.Value);
        Assert.Null(result.TraceId);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Disabled, result.Link.Status);
    }

    private static LangfuseExperimentRun CreateRun(HttpClient httpClient) =>
        new(
            new LangfuseApiClient(httpClient, BaseUrl, "Basic x"),
            LangfuseTestFactory.OkScoreRecorder(),
            datasetName: "evals",
            runName: "run-abc",
            options: null,
            diagnostics: null);
}
