using System.Net;
using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

using Moq;

using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

[Collection("Langfuse activity")]
public sealed class LangfuseExperimentResultSinkTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    [Fact]
    public async Task RunAsync_ProjectsItemRunAndDecisionScoresWithContextualIds()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(captured);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var policy = new CountingExperimentRunPolicy<int, string>(
            EvaluationDecision.Failed);
        var sink = client.CreateExperimentResultSink<int, string>(
            run,
            new LangfuseExperimentResultSinkOptions<int, string>
            {
                ItemScoreIdProvider = (item, metric) =>
                    $"{item.Case.Id}:{item.TrialIndex}:{metric.Name}",
                RunEvaluationScoreIdProvider = (evaluation, metric) =>
                    $"{evaluation.Name}:{metric.Name}",
                DecisionScore = new LangfuseExperimentDecisionScoreOptions
                {
                    Name = "experiment_decision",
                    ScoreIdProvider = decision => $"decision:{decision}",
                    Comment = "Canonical Needlr decision",
                },
            });
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies: [policy],
            trialCount: 2,
            itemEvaluator: (context, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("quality", context.TrialIndex))),
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, string>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("quality", 0.5)))),
            ]);

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 2 },
            _cancellationToken);

        Assert.Equal(1, policy.InvocationCount);
        Assert.Equal(ExperimentRunDecision.Failed, outcome.Result.Decision);
        Assert.Equal(ExperimentPublicationStatus.Succeeded, outcome.PublicationStatus);
        var sinkResult = Assert.Single(outcome.SinkResults);
        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, sinkResult.Status);

        var scoreRequests = captured
            .Where(request => request.Uri.AbsolutePath.EndsWith(
                "/scores",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, scoreRequests.Length);
        var scoreJson = scoreRequests
            .Select(request => JsonDocument.Parse(request.Body!))
            .ToArray();
        try
        {
            var itemScores = scoreJson
                .Where(json => json.RootElement.TryGetProperty("traceId", out _))
                .ToArray();
            Assert.Equal(2, itemScores.Length);
            Assert.Equal(
                ["case-1:1:quality", "case-1:2:quality"],
                itemScores
                    .Select(json => json.RootElement.GetProperty("id").GetString())
                    .OrderBy(id => id, StringComparer.Ordinal));
            var itemTraceIds = outcome.Result.Items
                .Select(GetTraceId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                itemTraceIds,
                itemScores
                    .Select(json => json.RootElement.GetProperty("traceId").GetString())
                    .OrderBy(id => id, StringComparer.Ordinal));

            var runScores = scoreJson
                .Where(json => json.RootElement.TryGetProperty("datasetRunId", out _))
                .ToArray();
            Assert.Equal(2, runScores.Length);
            Assert.All(
                runScores,
                json => Assert.Equal(
                    "dataset-run-1",
                    json.RootElement.GetProperty("datasetRunId").GetString()));
            var aggregate = Assert.Single(
                runScores,
                json => json.RootElement.GetProperty("name").GetString() == "quality");
            Assert.Equal(
                "aggregate:quality",
                aggregate.RootElement.GetProperty("id").GetString());
            var decision = Assert.Single(
                runScores,
                json => json.RootElement.GetProperty("name").GetString() == "experiment_decision");
            Assert.Equal("decision:Failed", decision.RootElement.GetProperty("id").GetString());
            Assert.Equal("Failed", decision.RootElement.GetProperty("value").GetString());
            Assert.Equal("CATEGORICAL", decision.RootElement.GetProperty("dataType").GetString());
            Assert.Equal(
                "Canonical Needlr decision",
                decision.RootElement.GetProperty("comment").GetString());
        }
        finally
        {
            foreach (var json in scoreJson)
            {
                json.Dispose();
            }
        }

        var snapshot = sink.GetPublicationSnapshot();
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Complete, snapshot.ScorePublicationStatus);
        Assert.Equal(2, snapshot.ItemScores.Count);
        Assert.All(
            snapshot.ItemScores,
            score => Assert.Equal(LangfuseExperimentScoreStatus.Accepted, score.Status));
        var runEvaluationScore = Assert.Single(snapshot.RunEvaluationScores);
        Assert.Equal("aggregate", runEvaluationScore.EvaluatorName);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, runEvaluationScore.Status);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, snapshot.DecisionScore!.Status);
        Assert.Equal(2, snapshot.ExperimentRunPublication!.RunScores.Accepted);
    }

    [Fact]
    public async Task RunAsync_LinkFailureDoesNotDoubleCountSuccessfulTraceScoreSink()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(
            captured,
            linkResponse: _ => LangfuseHttpStub.Json(
                HttpStatusCode.BadRequest,
                "missing dataset item"));
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var sink = client.CreateExperimentResultSink<int, string>(run);
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies: [],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("quality", 1))));

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            outcome.Result.Items.Single().Publications.Single().Status);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Succeeded,
            outcome.SinkResults.Single().Status);
        var snapshot = sink.GetPublicationSnapshot();
        var itemScore = Assert.Single(snapshot.ItemScores);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, itemScore.Status);
        Assert.Equal(1, snapshot.ExperimentRunPublication!.ItemLinks.Failed);
    }

    [Theory]
    [InlineData(false, ExperimentPublicationStatus.PartiallyFailed)]
    [InlineData(true, ExperimentPublicationStatus.Failed)]
    public async Task RunAsync_UnresolvedIdentityFailsConfiguredRunScoresWithoutChangingQuality(
        bool isRequired,
        ExperimentPublicationStatus expectedPublicationStatus)
    {
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var policy = new CountingExperimentRunPolicy<int, string>(
            EvaluationDecision.Passed);
        var sink = client.CreateExperimentResultSink<int, string>(
            run,
            new LangfuseExperimentResultSinkOptions<int, string>
            {
                IsRequired = isRequired,
                DecisionScore = new LangfuseExperimentDecisionScoreOptions
                {
                    Name = "experiment_decision",
                },
            });
        var definition = CreateDefinition(
            itemScopes: [],
            sinks: [sink],
            policies: [policy],
            trialCount: 1,
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, string>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("quality", 0.5)))),
            ]);

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(1, policy.InvocationCount);
        Assert.Equal(ExperimentRunDecision.Passed, outcome.Result.Decision);
        Assert.Equal(expectedPublicationStatus, outcome.PublicationStatus);
        var sinkResult = Assert.Single(outcome.SinkResults);
        Assert.Equal(sink.Name, sinkResult.Name);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, sinkResult.Status);
        Assert.Equal(isRequired, sinkResult.IsRequired);
        Assert.Empty(handler.CapturedRequests);
        var snapshot = sink.GetPublicationSnapshot();
        var runScore = Assert.Single(snapshot.RunEvaluationScores);
        Assert.Equal(LangfuseExperimentScoreStatus.NotAttempted, runScore.Status);
        Assert.NotNull(runScore.Failure);
        Assert.Equal(
            LangfusePublicationFailureCode.DatasetRunIdentityUnavailable,
            runScore.Failure!.Code);
        Assert.Equal(
            LangfuseExperimentScoreStatus.NotAttempted,
            snapshot.DecisionScore!.Status);
        Assert.Equal(
            LangfuseDatasetRunIdentityStatus.Unresolved,
            snapshot.ExperimentRunPublication!.IdentityStatus);
    }

    [Fact]
    public async Task RunAsync_InconsistentIdentityFailsRunEvaluationPublication()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(
            captured,
            datasetRunId: call => call == 1 ? "dataset-run-1" : "dataset-run-2");
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var sink = client.CreateExperimentResultSink<int, string>(run);
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies: [],
            trialCount: 2,
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, string>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("quality", 0.5)))),
            ]);

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(LangfuseDatasetRunIdentityStatus.Inconsistent, run.IdentityStatus);
        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            outcome.SinkResults.Single().Status);
        var snapshot = sink.GetPublicationSnapshot();
        var score = Assert.Single(snapshot.RunEvaluationScores);
        Assert.Equal(LangfuseExperimentScoreStatus.NotAttempted, score.Status);
        Assert.Equal(
            LangfusePublicationFailureCode.InconsistentDatasetRunIdentity,
            score.Failure!.Code);
        Assert.Equal(
            LangfuseDatasetRunIdentityStatus.Inconsistent,
            snapshot.ExperimentRunPublication!.IdentityStatus);
    }

    [Fact]
    public async Task RunAsync_LocalSinkPublishesItemScoresAndSkipsRunTargets()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(captured);
        var client = CreateClient(httpClient);
        var scope = client.CreateLocalExperimentItemScopeProvider<int, string>();
        var sink = client.CreateLocalExperimentResultSink<int, string>(
            new LangfuseExperimentResultSinkOptions<int, string>
            {
                DecisionScore = new LangfuseExperimentDecisionScoreOptions
                {
                    Name = "experiment_decision",
                },
            });
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies:
            [
                new CountingExperimentRunPolicy<int, string>(
                    EvaluationDecision.Passed),
            ],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("quality", 1))),
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, string>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("quality", 0.5)))),
            ]);

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(ExperimentPublicationStatus.Succeeded, outcome.PublicationStatus);
        Assert.Single(
            captured,
            request => request.Uri.AbsolutePath.EndsWith("/scores", StringComparison.Ordinal));
        var snapshot = sink.GetPublicationSnapshot();
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Complete, snapshot.ScorePublicationStatus);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, snapshot.ItemScores.Single().Status);
        Assert.Equal(
            LangfuseExperimentScoreStatus.NotAttempted,
            snapshot.RunEvaluationScores.Single().Status);
        Assert.Equal(
            LangfuseExperimentScoreStatus.NotAttempted,
            snapshot.DecisionScore!.Status);
        Assert.Null(snapshot.ExperimentRunPublication);
    }

    [Fact]
    public async Task RunAsync_DisabledHostedSinkIsCoherentNoOpForSameDefinitionShape()
    {
        var client = new DisabledLangfuseClient();
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var sink = client.CreateExperimentResultSink<int, string>(
            run,
            new LangfuseExperimentResultSinkOptions<int, string>
            {
                DecisionScore = new LangfuseExperimentDecisionScoreOptions
                {
                    Name = "experiment_decision",
                },
            });
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies:
            [
                new CountingExperimentRunPolicy<int, string>(
                    EvaluationDecision.Passed),
            ],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("quality", 1))),
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, string>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("quality", 0.5)))),
            ]);

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(ExperimentRunDecision.Passed, outcome.Result.Decision);
        Assert.Equal(ExperimentPublicationStatus.NotRequested, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentPublicationOperationStatus.NotAttempted,
            outcome.SinkResults.Single().Status);
        var snapshot = sink.GetPublicationSnapshot();
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Disabled, snapshot.ScorePublicationStatus);
        Assert.Equal(LangfuseExperimentScoreStatus.Disabled, snapshot.ItemScores.Single().Status);
        Assert.Equal(
            LangfuseExperimentScoreStatus.Disabled,
            snapshot.RunEvaluationScores.Single().Status);
        Assert.Equal(LangfuseExperimentScoreStatus.Disabled, snapshot.DecisionScore!.Status);
        Assert.Equal(
            LangfuseDatasetRunIdentityStatus.Disabled,
            snapshot.ExperimentRunPublication!.IdentityStatus);
    }

    [Fact]
    public async Task RunAsync_EnabledMissingTraceFailsRequestedItemScore()
    {
        var handler = new TrackingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);
        var scope = client.CreateLocalExperimentItemScopeProvider<int, string>();
        var sink = client.CreateLocalExperimentResultSink<int, string>();
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies: [],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("quality", 1))));

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Empty(handler.CapturedRequests);
        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            outcome.SinkResults.Single().Status);
        var score = Assert.Single(sink.GetPublicationSnapshot().ItemScores);
        Assert.Equal(LangfuseExperimentScoreStatus.Failed, score.Status);
        Assert.Equal(
            LangfusePublicationFailureCode.TraceUnavailable,
            score.Failure!.Code);
    }

    [Fact]
    public async Task RunAsync_StrictRunEvaluationFailureRemainsInSnapshot()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(
            captured,
            scoreResponse: _ => LangfuseHttpStub.Json(
                HttpStatusCode.InternalServerError,
                "run score failure"));
        var client = CreateClient(httpClient, LangfuseScoreFailureMode.Strict);
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var sink = client.CreateExperimentResultSink<int, string>(run);
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies: [],
            trialCount: 1,
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, string>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("quality", 0.5)))),
            ]);

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            outcome.SinkResults.Single().Status);
        var score = Assert.Single(
            sink.GetPublicationSnapshot().RunEvaluationScores);
        Assert.Equal(LangfuseExperimentScoreStatus.Failed, score.Status);
        Assert.Contains("run score failure", score.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_StrictDecisionFailureRemainsInSnapshot()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(
            captured,
            scoreResponse: _ => LangfuseHttpStub.Json(
                HttpStatusCode.InternalServerError,
                "decision failure"));
        var client = CreateClient(httpClient, LangfuseScoreFailureMode.Strict);
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var sink = client.CreateExperimentResultSink<int, string>(
            run,
            new LangfuseExperimentResultSinkOptions<int, string>
            {
                DecisionScore = new LangfuseExperimentDecisionScoreOptions
                {
                    Name = "experiment_decision",
                },
            });
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies:
            [
                new CountingExperimentRunPolicy<int, string>(
                    EvaluationDecision.Passed),
            ],
            trialCount: 1);

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(ExperimentRunDecision.Passed, outcome.Result.Decision);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            outcome.SinkResults.Single().Status);
        var score = sink.GetPublicationSnapshot().DecisionScore;
        Assert.NotNull(score);
        Assert.Equal(LangfuseExperimentScoreStatus.Failed, score!.Status);
        Assert.Contains("decision failure", score.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_CancellationRetainsAcceptedAndCanceledItemScoreDetails()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var secondScoreStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scoreCalls = 0;
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                if (Interlocked.Increment(ref scoreCalls) == 1)
                {
                    return LangfuseHttpStub.ScoreAccepted(request);
                }

                secondScoreStarted.TrySetResult();
                return await pendingResponse.Task.WaitAsync(cancellationToken);
            }));
        var client = CreateClient(httpClient);
        var scope = client.CreateLocalExperimentItemScopeProvider<int, string>();
        var sink = client.CreateLocalExperimentResultSink<int, string>();
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies: [],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("first", 1),
                    new NumericMetric("second", 2))));
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

        var runTask = new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await secondScoreStarted.Task.WaitAsync(_cancellationToken);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        var snapshot = sink.GetPublicationSnapshot();
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Partial, snapshot.ScorePublicationStatus);
        Assert.Equal(2, snapshot.ItemScores.Count);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, snapshot.ItemScores[0].Status);
        Assert.Equal(LangfuseExperimentScoreStatus.NotAttempted, snapshot.ItemScores[1].Status);
        Assert.Equal(
            LangfusePublicationFailureCode.PublicationCanceled,
            snapshot.ItemScores[1].Failure!.Code);
    }

    [Fact]
    public async Task RunAsync_CancellationBackfillsPendingRunAndDecisionScores()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var runScoreStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new List<CapturedRequest>();
        var scoreCalls = 0;
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                var body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                var capturedRequest = new CapturedRequest(
                    request.Method,
                    request.RequestUri!,
                    body);
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

                if (Interlocked.Increment(ref scoreCalls) == 1)
                {
                    return LangfuseHttpStub.ScoreAccepted(request);
                }

                runScoreStarted.TrySetResult();
                return await pendingResponse.Task.WaitAsync(cancellationToken);
            }));
        var client = CreateClient(httpClient);
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var sink = client.CreateExperimentResultSink<int, string>(
            run,
            new LangfuseExperimentResultSinkOptions<int, string>
            {
                DecisionScore = new LangfuseExperimentDecisionScoreOptions
                {
                    Name = "experiment_decision",
                },
            });
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies:
            [
                new CountingExperimentRunPolicy<int, string>(
                    EvaluationDecision.Passed),
            ],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("item_quality", 1))),
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, string>(
                    "first-run-evaluator",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("first_run_quality", 1)))),
                new ExperimentRunEvaluator<int, string>(
                    "second-run-evaluator",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("second_run_quality", 1)))),
            ]);
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

        var runTask = new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await runScoreStarted.Task.WaitAsync(_cancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        var snapshot = sink.GetPublicationSnapshot();
        Assert.Equal(LangfuseExperimentApiPublicationStatus.Partial, snapshot.ScorePublicationStatus);
        Assert.Equal(LangfuseExperimentScoreStatus.Accepted, snapshot.ItemScores.Single().Status);
        Assert.Equal(2, snapshot.RunEvaluationScores.Count);
        Assert.All(snapshot.RunEvaluationScores, score =>
        {
            Assert.Equal(LangfuseExperimentScoreStatus.NotAttempted, score.Status);
            Assert.Equal(
                LangfusePublicationFailureCode.PublicationCanceled,
                score.Failure!.Code);
        });
        Assert.Equal(
            LangfusePublicationFailureCode.PublicationCanceled,
            snapshot.DecisionScore!.Failure!.Code);
    }

    [Fact]
    public async Task PublishAsync_ConcurrentCallIsRejectedAndLaterCallCanRecover()
    {
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                requestStarted.TrySetResult();
                await releaseResponse.Task.WaitAsync(cancellationToken);
                return LangfuseHttpStub.ScoreAccepted(request);
            }));
        var client = CreateClient(httpClient);
        var sink = client.CreateLocalExperimentResultSink<int, string>();
        var result = CreateCanonicalResult();

        var firstPublication = sink.PublishAsync(
            result,
            _cancellationToken).AsTask();
        await requestStarted.Task.WaitAsync(_cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sink.PublishAsync(result, _cancellationToken).AsTask());
        releaseResponse.TrySetResult();
        var firstResult = await firstPublication;
        var secondResult = await sink.PublishAsync(
            result,
            _cancellationToken);

        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, firstResult.Status);
        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, secondResult.Status);
        Assert.Equal(
            LangfuseExperimentApiPublicationStatus.Complete,
            sink.GetPublicationSnapshot().ScorePublicationStatus);
    }

    [Fact]
    public async Task RunAsync_InvalidScoreIdCallbackLeavesFailedProviderSnapshot()
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(captured);
        var client = CreateClient(httpClient);
        var scope = client.CreateLocalExperimentItemScopeProvider<int, string>();
        var sink = client.CreateLocalExperimentResultSink<int, string>(
            new LangfuseExperimentResultSinkOptions<int, string>
            {
                ItemScoreIdProvider = (_, _) => " ",
            });
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [sink],
            policies: [],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("quality", 1))));

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Empty(captured);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            outcome.SinkResults.Single().Status);
        Assert.Equal(
            LangfuseExperimentApiPublicationStatus.Failed,
            sink.GetPublicationSnapshot().ScorePublicationStatus);
    }

    [Theory]
    [InlineData(LangfuseScoreFailureMode.NonFatal)]
    [InlineData(LangfuseScoreFailureMode.Strict)]
    public async Task RunAsync_FailingLangfuseSinkDoesNotSuppressLaterSink(
        LangfuseScoreFailureMode failureMode)
    {
        using var listener = LangfuseTestFactory.StartListener();
        var captured = new List<CapturedRequest>();
        using var httpClient = CreatePublishingHttpClient(
            captured,
            scoreResponse: _ => LangfuseHttpStub.Json(
                HttpStatusCode.InternalServerError,
                "score failure"));
        var client = CreateClient(httpClient, failureMode);
        var run = client.BeginExperimentRun("evals", "run-1");
        var scope = client.CreateExperimentItemScopeProvider<int, string>(run);
        var langfuseSink = client.CreateExperimentResultSink<int, string>(run);
        var laterSink = _mocks.Create<IExperimentResultSink<int, string>>();
        laterSink
            .SetupGet(sink => sink.Name)
            .Returns("later");
        laterSink
            .SetupGet(sink => sink.IsRequired)
            .Returns(false);
        laterSink
            .Setup(sink => sink.PublishAsync(
                It.Is<ExperimentRunResult<int, string>>(
                    result => result.Decision == ExperimentRunDecision.Passed),
                _cancellationToken))
            .Returns(ValueTask.FromResult(
                ExperimentSinkPublicationOperationResult.Succeeded()));
        var definition = CreateDefinition(
            itemScopes: [scope],
            sinks: [langfuseSink, laterSink.Object],
            policies:
            [
                new CountingExperimentRunPolicy<int, string>(
                    EvaluationDecision.Passed),
            ],
            trialCount: 1,
            itemEvaluator: (_, _) =>
                ValueTask.FromResult(new EvaluationResult(
                    new NumericMetric("quality", 1))));

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(ExperimentRunDecision.Passed, outcome.Result.Decision);
        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
        Assert.Equal(2, outcome.SinkResults.Count);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            outcome.SinkResults[0].Status);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Succeeded,
            outcome.SinkResults[1].Status);
        Assert.Equal(
            LangfuseExperimentScoreStatus.Failed,
            langfuseSink.GetPublicationSnapshot().ItemScores.Single().Status);
        _mocks.VerifyAll();
    }

    private static ILangfuseClient CreateClient(
        HttpClient httpClient,
        LangfuseScoreFailureMode failureMode = LangfuseScoreFailureMode.NonFatal)
    {
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            Host = BaseUrl.AbsoluteUri,
            ScoreFailureMode = failureMode,
        };
        var transport = new LangfuseHttpTransport(httpClient);
        return new LangfuseClient(
            transport,
            LangfuseEndpoints.Resolve(options),
            options);
    }

    private static HttpClient CreatePublishingHttpClient(
        List<CapturedRequest> captured,
        Func<int, string>? datasetRunId = null,
        Func<CapturedRequest, HttpResponseMessage>? linkResponse = null,
        Func<CapturedRequest, HttpResponseMessage>? scoreResponse = null)
    {
        datasetRunId ??= _ => "dataset-run-1";
        var linkCalls = 0;
        return new HttpClient(new DelegateHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                var body = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                var capturedRequest = new CapturedRequest(
                    request.Method,
                    request.RequestUri!,
                    body);
                captured.Add(capturedRequest);
                if (request.RequestUri!.AbsolutePath.EndsWith(
                    "/dataset-run-items",
                    StringComparison.Ordinal))
                {
                    var call = Interlocked.Increment(ref linkCalls);
                    return linkResponse?.Invoke(capturedRequest)
                        ?? LangfuseDatasetRunItemHttpStub.CreateResponse(
                            capturedRequest,
                            $"dataset-run-item-{call}",
                            datasetRunId(call));
                }

                return scoreResponse?.Invoke(capturedRequest)
                    ?? LangfuseHttpStub.ScoreAccepted(request);
            }));
    }

    private static ExperimentDefinition<int, string> CreateDefinition(
        IReadOnlyList<IExperimentItemScopeProvider<int, string>> itemScopes,
        IReadOnlyList<IExperimentResultSink<int, string>> sinks,
        IReadOnlyList<IExperimentRunPolicy<int, string>> policies,
        int trialCount,
        ExperimentItemEvaluator<int, string>? itemEvaluator = null,
        IReadOnlyList<IExperimentRunEvaluator<int, string>>? runEvaluators = null) =>
        new()
        {
            Name = "langfuse-result-sink",
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
            ItemScopes = itemScopes,
            Sinks = sinks,
            Task = (context, _) =>
                ValueTask.FromResult($"trial-{context.TrialIndex}"),
            ItemEvaluator = itemEvaluator,
            RunEvaluators = runEvaluators ?? [],
            Policies = policies,
        };

    private static string GetTraceId(ExperimentItemResult<int, string> item) =>
        item.Publications
            .Single(publication =>
                publication.Name
                == LangfuseExperimentItemScopeProvider<int, string>.ProviderName)
            .Correlations
            .Single(correlation =>
                correlation.Namespace
                    == LangfuseExperimentItemScopeProvider<int, string>.CorrelationNamespace
                && correlation.Name
                    == LangfuseExperimentItemScopeProvider<int, string>.TraceIdCorrelationName)
            .Value;

    private static ExperimentRunResult<int, string> CreateCanonicalResult() =>
        new(
            "run-1",
            "direct-publication",
            new ExperimentSourceReference { Name = "local" },
            new DateTimeOffset(
                2026,
                7,
                15,
                0,
                0,
                0,
                TimeSpan.Zero),
            TimeSpan.FromSeconds(1),
            1,
            1,
            [
                ExperimentItemResult<int, string>.Succeeded(
                    0,
                    new ExperimentCase<int>
                    {
                        Id = "case-1",
                        Value = 1,
                    },
                    1,
                    [
                        ExperimentAttemptResult.Succeeded(
                            1,
                            new DateTimeOffset(
                                2026,
                                7,
                                15,
                                0,
                                0,
                                0,
                                TimeSpan.Zero),
                            TimeSpan.FromSeconds(1)),
                    ],
                    "done",
                    new EvaluationResult(
                        new NumericMetric("quality", 1)),
                    [
                        ExperimentItemPublicationResult.Succeeded(
                            LangfuseExperimentItemScopeProvider<int, string>.ProviderName,
                            false,
                            [
                                new ExperimentItemCorrelation
                                {
                                    Namespace =
                                        LangfuseExperimentItemScopeProvider<int, string>.CorrelationNamespace,
                                    Name =
                                        LangfuseExperimentItemScopeProvider<int, string>.TraceIdCorrelationName,
                                    Value = "trace-1",
                                },
                            ]),
                    ]),
            ],
            [],
            [
                ExperimentPolicyResult.FromVerdict(
                    "decision",
                    ExperimentPolicyKind.Deterministic,
                    isRequired: true,
                    ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Passed)),
            ]);
}
