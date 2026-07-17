using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Time.Testing;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseExperimentItemScopeProviderTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_RetryAndEvaluationReactivateOneLinkedTrialTrace()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var caller = new Activity("caller").Start();
        var stoppedActivities = 0;
        using var stoppedListener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var client = CreateClient(httpClient);
        var version = new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.FromHours(-4));
        var run = client.BeginExperimentRun(
            "evals",
            "run-1",
            new LangfuseExperimentRunOptions { DatasetVersion = version });
        var provider = client.CreateExperimentItemScopeProvider<int, string>(
            run,
            new LangfuseExperimentItemScopeOptions<int>
            {
                ScenarioNameFactory = _ => "evaluate-experiment-item",
                Tags = ["experiment"],
                Metadata = new Dictionary<string, string>
                {
                    ["candidate"] = "test",
                },
            });
        var timeProvider = new FakeTimeProvider();
        var firstAttemptFinished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var traceIds = new ConcurrentQueue<string>();
        var attempts = 0;
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            async (context, _) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Same(scenario.Activity, Activity.Current);
                Assert.Equal(
                    "evaluate-experiment-item",
                    scenario.Activity!.GetTagItem("langfuse.trace.name"));
                Assert.Equal(
                    "test",
                    scenario.Activity.GetTagItem("langfuse.trace.metadata.candidate"));
                Assert.Equal(
                    ["experiment"],
                    Assert.IsType<string[]>(scenario.Activity.GetTagItem("langfuse.trace.tags")));
                traceIds.Enqueue(scenario.TraceId!);
                await Task.Yield();
                Assert.Same(scenario.Activity, Activity.Current);
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    firstAttemptFinished.TrySetResult();
                    throw new InvalidOperationException("retry");
                }

                return "completed";
            },
            (context, _) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Same(scenario.Activity, Activity.Current);
                traceIds.Enqueue(scenario.TraceId!);
                return ValueTask.FromResult(new EvaluationResult());
            });
        var runTask = new ExperimentRunner(timeProvider).RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 2,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.FromMinutes(1)),
            },
            _cancellationToken);
        await firstAttemptFinished.Task.WaitAsync(_cancellationToken);
        await AdvanceUntilAsync(
            timeProvider,
            () => Volatile.Read(ref attempts) == 2,
            TimeSpan.FromMinutes(1));

        var outcome = await runTask;

        Assert.Equal(2, attempts);
        Assert.Equal(3, traceIds.Count);
        Assert.Single(traceIds.Distinct(StringComparer.Ordinal));
        Assert.Equal(1, stoppedActivities);
        Assert.Same(caller, Activity.Current);
        var linkRequest = Assert.Single(captured);
        using var linkJson = JsonDocument.Parse(linkRequest.Body!);
        Assert.Equal("2026-07-01T12:30:00+00:00", linkJson.RootElement.GetProperty("datasetVersion").GetString());
        Assert.False(linkJson.RootElement.TryGetProperty("createdAt", out _));

        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(2, item.Attempts.Count);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        var publication = Assert.Single(item.Publications);
        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, publication.Status);
        Assert.Equal(3, publication.Correlations.Count);
        AssertCorrelation(
            publication.Correlations[0],
            LangfuseExperimentItemScopeProvider<int, string>.TraceIdCorrelationName,
            traceIds.First());
        AssertCorrelation(
            publication.Correlations[1],
            LangfuseExperimentItemScopeProvider<int, string>.DatasetRunItemIdCorrelationName,
            "dataset-run-item-1");
        AssertCorrelation(
            publication.Correlations[2],
            LangfuseExperimentItemScopeProvider<int, string>.DatasetRunIdCorrelationName,
            "dataset-run-1");
        Assert.Equal("dataset-run-1", run.DatasetRunId);
    }

    [Fact]
    public async Task RunAsync_ParallelTrialsUseDistinctIsolatedTracesAndRestoreCaller()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var caller = new Activity("caller").Start();
        var stoppedActivities = 0;
        using var stoppedListener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", captured);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(run);
        var bothEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var definition = CreateDefinition(
            provider,
            trialCount: 2,
            async (context, cancellationToken) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Same(scenario.Activity, Activity.Current);
                if (Interlocked.Increment(ref entered) == 2)
                {
                    bothEntered.TrySetResult();
                }

                await bothEntered.Task.WaitAsync(cancellationToken);
                Assert.Same(scenario.Activity, Activity.Current);
                return scenario.TraceId!;
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 2 },
            _cancellationToken);

        Assert.Equal(2, outcome.Result.Items.Count);
        Assert.Equal(2, outcome.Result.Items.Select(item => item.Output).Distinct().Count());
        Assert.Equal(2, captured.Count);
        Assert.Equal(2, stoppedActivities);
        Assert.Same(caller, Activity.Current);
        Assert.All(outcome.Result.Items, item =>
        {
            var publication = Assert.Single(item.Publications);
            Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, publication.Status);
            Assert.Equal(3, publication.Correlations.Count);
        });
    }

    [Fact]
    public async Task RunAsync_LocalProviderCreatesTraceWithoutDatasetLink()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);
        var provider = client.CreateLocalExperimentItemScopeProvider<int, string>();
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            (context, _) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Same(scenario.Activity, Activity.Current);
                using var child = LangfuseActivitySource.Source.StartActivity("agent.tool")!;
                Assert.Equal(scenario.Activity!.TraceId, child.TraceId);
                return ValueTask.FromResult(scenario.TraceId!);
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Empty(handler.CapturedRequests);
        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(item.Output, item.Correlations.Single().Value);
        var publication = Assert.Single(item.Publications);
        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, publication.Status);
        var correlation = Assert.Single(publication.Correlations);
        AssertCorrelation(
            correlation,
            LangfuseExperimentItemScopeProvider<int, string>.TraceIdCorrelationName,
            item.Output!);
    }

    [Fact]
    public async Task RunAsync_BestEffortLinkFailurePreservesQualityAndFailsRequiredPublication()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "missing item"),
            []);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(
            run,
            new LangfuseExperimentItemScopeOptions<int>
            {
                IsRequired = true,
                FailureMode = ExperimentItemScopeFailureMode.BestEffort,
            });
        var taskInvocations = 0;
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            (_, _) =>
            {
                Interlocked.Increment(ref taskInvocations);
                return ValueTask.FromResult("quality-result");
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(1, taskInvocations);
        Assert.Equal(ExperimentPublicationStatus.Failed, outcome.PublicationStatus);
        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal("quality-result", item.Output);
        var publication = Assert.Single(item.Publications);
        Assert.True(publication.IsRequired, "Expected required publication to affect aggregate publication health.");
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, publication.Status);
        Assert.Equal(ExperimentFailureCode.ItemScopeFailed, publication.Failure!.Code);
        Assert.Contains("missing item", publication.Failure.Message, StringComparison.Ordinal);
        var trace = Assert.Single(publication.Correlations);
        Assert.Equal(
            LangfuseExperimentItemScopeProvider<int, string>.TraceIdCorrelationName,
            trace.Name);
        Assert.Equal(1, run.GetPublicationSnapshot().ItemLinks.Failed);
    }

    [Fact]
    public async Task RunAsync_StrictLinkFailurePreventsAttemptAndDisposesScenario()
    {
        var stoppedActivities = 0;
        using var listener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "missing item"),
            []);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(
            run,
            new LangfuseExperimentItemScopeOptions<int>
            {
                FailureMode = ExperimentItemScopeFailureMode.ExecutionPrerequisite,
            });
        var taskInvocations = 0;
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            (_, _) =>
            {
                Interlocked.Increment(ref taskInvocations);
                return ValueTask.FromResult("not reached");
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(0, taskInvocations);
        Assert.Equal(1, stoppedActivities);
        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(ExperimentItemStatus.PrerequisiteFailed, item.Status);
        Assert.Empty(item.Attempts);
        var publication = Assert.Single(item.Publications);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, publication.Status);
        Assert.Equal(ExperimentFailureCode.ItemScopeFailed, publication.Failure!.Code);
        Assert.Equal(1, run.GetPublicationSnapshot().ItemLinks.Failed);
    }

    [Fact]
    public async Task RunAsync_InconsistentDatasetRunIdentityIsStructuredPerTrial()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create(
            call => call == 1 ? "dataset-run-1" : "dataset-run-2",
            captured);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(run);
        var definition = CreateDefinition(
            provider,
            trialCount: 2,
            (context, _) => ValueTask.FromResult($"trial-{context.TrialIndex}"));

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(2, captured.Count);
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Inconsistent, run.IdentityStatus);
        Assert.Null(run.DatasetRunId);
        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Succeeded,
            outcome.Result.Items[0].Publications.Single().Status);
        var inconsistent = outcome.Result.Items[1].Publications.Single();
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, inconsistent.Status);
        Assert.Equal(3, inconsistent.Correlations.Count);
        AssertCorrelation(
            inconsistent.Correlations[2],
            LangfuseExperimentItemScopeProvider<int, string>.DatasetRunIdCorrelationName,
            "dataset-run-2");
    }

    [Fact]
    public async Task RunAsync_NotSampledHostedScopeDoesNotBlockStrictExecution()
    {
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(
            run,
            new LangfuseExperimentItemScopeOptions<int>
            {
                FailureMode = ExperimentItemScopeFailureMode.ExecutionPrerequisite,
            });
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            (context, _) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Null(scenario.Activity);
                return ValueTask.FromResult("continued");
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Empty(handler.CapturedRequests);
        Assert.Equal(ExperimentPublicationStatus.NotRequested, outcome.PublicationStatus);
        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal("continued", item.Output);
        var publication = Assert.Single(item.Publications);
        Assert.Equal(ExperimentPublicationOperationStatus.NotAttempted, publication.Status);
        Assert.Empty(publication.Correlations);
        Assert.Equal(1, run.GetPublicationSnapshot().ItemLinks.NotSampled);
    }

    [Fact]
    public async Task RunAsync_DisabledHostedScopeIsCoherentNoOp()
    {
        var client = new DisabledLangfuseClient();
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(run);
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            (context, _) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Null(scenario.Activity);
                Assert.Null(scenario.TraceId);
                return ValueTask.FromResult("continued");
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(ExperimentPublicationStatus.NotRequested, outcome.PublicationStatus);
        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        var publication = Assert.Single(item.Publications);
        Assert.Equal(ExperimentPublicationOperationStatus.NotAttempted, publication.Status);
        Assert.Empty(publication.Correlations);
        var snapshot = run.GetPublicationSnapshot();
        Assert.Equal(LangfuseDatasetRunIdentityStatus.Disabled, snapshot.IdentityStatus);
        Assert.Equal(1, snapshot.ItemLinks.Disabled);
    }

    [Fact]
    public async Task RunAsync_ItemEvaluatorCanPublishScoreBeforeScenarioDisposal()
    {
        var stoppedActivities = 0;
        using var listener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        var captured = new List<CapturedRequest>();
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                var body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                var capturedRequest = new CapturedRequest(request.Method, request.RequestUri!, body);
                captured.Add(capturedRequest);
                if (request.RequestUri!.AbsolutePath.EndsWith(
                    "/dataset-run-items",
                    StringComparison.Ordinal))
                {
                    return LangfuseDatasetRunItemHttpStub.CreateResponse(
                        capturedRequest,
                        "dataset-run-item-1",
                        "dataset-run-1");
                }

                Assert.Equal(0, Volatile.Read(ref stoppedActivities));
                return LangfuseHttpStub.ScoreAccepted(request);
            }));
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(run);
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            (_, _) => ValueTask.FromResult("completed"),
            async (context, cancellationToken) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Same(scenario.Activity, Activity.Current);
                Assert.Equal(0, Volatile.Read(ref stoppedActivities));
                await scenario.RecordScoreAsync(
                    "quality",
                    1.0,
                    options: null,
                    cancellationToken);
                Assert.Equal(0, Volatile.Read(ref stoppedActivities));
                return new EvaluationResult();
            });

        await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(1, stoppedActivities);
        Assert.Equal(2, captured.Count);
        Assert.Single(
            captured,
            request => request.Uri.AbsolutePath.EndsWith("/scores", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_CallerCancellationAbortsAndRestoresAmbientActivity()
    {
        using var listener = LangfuseTestFactory.StartListener();
        using var caller = new Activity("caller").Start();
        var stoppedActivities = 0;
        using var stoppedListener = LangfuseTestFactory.StartListener(
            onStopped: _ => Interlocked.Increment(ref stoppedActivities));
        using var httpClient = LangfuseDatasetRunItemHttpStub.Create("dataset-run-1", []);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var provider = client.CreateExperimentItemScopeProvider<int, string>(run);
        var taskStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingTask = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = CreateDefinition(
            provider,
            trialCount: 1,
            async (context, cancellationToken) =>
            {
                var scenario = context.Features.GetRequired<ILangfuseScenario>();
                Assert.Same(scenario.Activity, Activity.Current);
                taskStarted.TrySetResult();
                return await pendingTask.Task.WaitAsync(cancellationToken);
            });
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

        var runTask = new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await taskStarted.Task.WaitAsync(_cancellationToken);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, stoppedActivities);
        Assert.Same(caller, Activity.Current);
        Assert.Equal(1, run.GetPublicationSnapshot().ItemLinks.Linked);
    }

    private static LangfuseClient CreateClient(HttpClient httpClient)
    {
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            Host = BaseUrl.AbsoluteUri,
        };
        var transport = new LangfuseHttpTransport(httpClient);
        return new LangfuseClient(
            transport,
            LangfuseEndpoints.Resolve(options),
            options);
    }

    private static ExperimentDefinition<int, string> CreateDefinition(
        IExperimentItemScopeProvider<int, string> provider,
        int trialCount,
        ExperimentTask<int, string> task,
        ExperimentItemEvaluator<int, string>? evaluator = null) =>
        new()
        {
            Name = "langfuse-scopes",
            CaseSource = new LocalExperimentCaseSource<int>(
                "cases",
                [
                    new ExperimentCase<int>
                    {
                        Id = "case-1",
                        Value = 1,
                        TrialCount = trialCount,
                    },
                ]),
            ItemScopes = [provider],
            Task = task,
            ItemEvaluator = evaluator,
        };

    private static void AssertCorrelation(
        ExperimentItemCorrelation correlation,
        string name,
        string value)
    {
        Assert.Equal(LangfuseExperimentItemScopeProvider<int, string>.CorrelationNamespace, correlation.Namespace);
        Assert.Equal(name, correlation.Name);
        Assert.Equal(value, correlation.Value);
    }

    private async Task AdvanceUntilAsync(
        FakeTimeProvider timeProvider,
        Func<bool> condition,
        TimeSpan step)
    {
        while (!condition())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(step);
            await Task.Yield();
        }
    }
}
