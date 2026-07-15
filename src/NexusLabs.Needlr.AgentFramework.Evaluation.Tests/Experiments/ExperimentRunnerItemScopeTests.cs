using System.Collections.Concurrent;

using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Time.Testing;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerItemScopeTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_ScopesSpanRetriesAndReactivateTaskAndEvaluator()
    {
        var timeProvider = new FakeTimeProvider();
        var attemptsByTrial = new ConcurrentDictionary<int, int>();
        var observations = new ConcurrentQueue<string>();
        var entries = 0;
        var activeActivations = 0;
        var completions = 0;
        var disposals = 0;
        var provider = new CallbackExperimentItemScopeProvider<int, int>(
            "recording",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (context, _) =>
            {
                Interlocked.Increment(ref entries);
                var feature = new ExperimentItemScopeTestFeature(
                    $"trial-{context.TrialIndex}");
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>
                        {
                            [typeof(ExperimentItemScopeTestFeature)] = feature,
                        },
                        () =>
                        {
                            var previous = feature.AmbientValue.Value;
                            feature.AmbientValue.Value = feature.Value;
                            Interlocked.Increment(ref activeActivations);
                            return new CallbackDisposable(() =>
                            {
                                feature.AmbientValue.Value = previous;
                                Interlocked.Decrement(ref activeActivations);
                            });
                        },
                        (item, _) =>
                        {
                            Interlocked.Increment(ref completions);
                            return ValueTask.FromResult(SucceededPublication(
                                "recording",
                                isRequired: false,
                                new ExperimentItemCorrelation
                                {
                                    Namespace = "recording",
                                    Name = "trial",
                                    Value = item.TrialIndex.ToString(),
                                }));
                        },
                        _ => ValueTask.CompletedTask,
                        () =>
                        {
                            Interlocked.Increment(ref disposals);
                            return ValueTask.CompletedTask;
                        });
                return ValueTask.FromResult(scope);
            });
        var definition = new ExperimentDefinition<int, int>
        {
            Name = "scoped-retries",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [
                    new ExperimentCase<int>
                    {
                        Id = "case-1",
                        Value = 1,
                        TrialCount = 2,
                    },
                ]),
            ItemScopes = [provider],
            Task = async (context, _) =>
            {
                var feature = context.Features
                    .GetRequired<ExperimentItemScopeTestFeature>();
                await Task.Yield();
                observations.Enqueue(
                    $"task:{context.TrialIndex}:{context.AttemptNumber}:" +
                    $"{feature.AmbientValue.Value}:{feature.Value}");
                attemptsByTrial.AddOrUpdate(
                    context.TrialIndex,
                    addValue: 1,
                    (_, count) => count + 1);
                return context.AttemptNumber == 1
                    ? throw new InvalidOperationException("retry")
                    : context.TrialIndex;
            },
            ItemEvaluator = (context, _) =>
            {
                var feature = context.Features
                    .GetRequired<ExperimentItemScopeTestFeature>();
                observations.Enqueue(
                    $"evaluation:{context.TrialIndex}:" +
                    $"{feature.AmbientValue.Value}:{feature.Value}");
                return ValueTask.FromResult(new EvaluationResult());
            },
        };
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
        await WaitUntilAsync(() =>
            attemptsByTrial.Count == 2
            && attemptsByTrial.Values.All(count => count == 1));

        Assert.Equal(2, entries);
        Assert.Equal(0, activeActivations);
        Assert.Equal(0, disposals);
        await AdvanceUntilAsync(
            timeProvider,
            () => attemptsByTrial.Values.Sum() == 4,
            TimeSpan.FromMinutes(1));
        var result = await runTask;

        Assert.Equal(2, result.Result.Items.Count);
        Assert.All(result.Result.Items, item => Assert.Equal(2, item.Attempts.Count));
        Assert.All(result.Result.Items, item =>
        {
            var publication = Assert.Single(item.Publications);
            Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, publication.Status);
            var correlation = Assert.Single(item.Correlations);
            Assert.Equal("recording", correlation.Namespace);
            Assert.Equal("trial", correlation.Name);
            Assert.Equal(item.TrialIndex.ToString(), correlation.Value);
        });
        Assert.Equal(6, observations.Count);
        Assert.All(
            observations,
            observation =>
            {
                var parts = observation.Split(':');
                Assert.Equal(parts[^1], parts[^2]);
            });
        Assert.Equal(2, completions);
        Assert.Equal(2, disposals);
        Assert.Equal(0, activeActivations);
    }

    [Fact]
    public async Task RunAsync_MultipleScopesUseNestedActivationAndReverseDisposal()
    {
        var events = new List<string>();
        var definition = CreateDefinition(
            task: (_, _) =>
            {
                events.Add("task");
                return ValueTask.FromResult(1);
            },
            evaluator: (_, _) =>
            {
                events.Add("evaluation");
                return ValueTask.FromResult(new EvaluationResult());
            },
            itemScopes:
            [
                CreateOrderedProvider("first", events),
                CreateOrderedProvider("second", events),
            ]);

        await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(
            [
                "enter:first",
                "enter:second",
                "activate:first",
                "activate:second",
                "task",
                "deactivate:second",
                "deactivate:first",
                "activate:first",
                "activate:second",
                "evaluation",
                "deactivate:second",
                "deactivate:first",
                "complete:first",
                "complete:second",
                "dispose:second",
                "dispose:first",
            ],
            events);
    }

    [Theory]
    [InlineData("success", ExperimentItemStatus.Succeeded)]
    [InlineData("execution", ExperimentItemStatus.ExecutionFailed)]
    [InlineData("canceled", ExperimentItemStatus.Canceled)]
    [InlineData("evaluation", ExperimentItemStatus.EvaluationFailed)]
    public async Task RunAsync_CompletionReceivesTerminalItemStatus(
        string scenario,
        ExperimentItemStatus expectedStatus)
    {
        ExperimentItemStatus? completedStatus = null;
        var provider = CreateCompletionProvider<int, int>(
            "completion",
            item => completedStatus = item.Status);
        var definition = CreateDefinition(
            task: (_, _) => scenario switch
            {
                "execution" => throw new InvalidOperationException("failed"),
                "canceled" => throw new OperationCanceledException("task canceled"),
                _ => ValueTask.FromResult(1),
            },
            evaluator: scenario == "evaluation"
                ? (_, _) => throw new InvalidOperationException("evaluation failed")
                : (_, _) => ValueTask.FromResult(new EvaluationResult()),
            itemScopes: [provider]);

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(expectedStatus, Assert.Single(result.Result.Items).Status);
        Assert.Equal(expectedStatus, completedStatus);
    }

    [Fact]
    public async Task RunAsync_CompletionReceivesTimedOutStatus()
    {
        var timeProvider = new FakeTimeProvider();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ExperimentItemStatus? completedStatus = null;
        var runTask = new ExperimentRunner(timeProvider).RunAsync(
            CreateDefinition(
                task: async (_, token) =>
                {
                    entered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, token);
                    return 1;
                },
                evaluator: null,
                itemScopes:
                [
                    CreateCompletionProvider<int, int>(
                        "completion",
                        item => completedStatus = item.Status),
                ]),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                AttemptTimeout = TimeSpan.FromSeconds(5),
            },
            _cancellationToken);
        await entered.Task.WaitAsync(_cancellationToken);

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        var result = await runTask;

        Assert.Equal(ExperimentItemStatus.TimedOut, Assert.Single(result.Result.Items).Status);
        Assert.Equal(ExperimentItemStatus.TimedOut, completedStatus);
    }

    [Fact]
    public async Task RunAsync_RetryPolicyFailureStillCompletesItemScope()
    {
        ExperimentFailureCode? completedFailure = null;
        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                task: (_, _) => throw new InvalidOperationException("execution failed"),
                evaluator: null,
                itemScopes:
                [
                    CreateCompletionProvider<int, int>(
                        "completion",
                        item => completedFailure = item.Failure?.Code),
                ]),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ThrowingExperimentRetryPolicy(),
            },
            _cancellationToken);

        Assert.Equal(
            ExperimentFailureCode.RetryPolicyFailed,
            Assert.Single(result.Result.Items).Failure!.Code);
        Assert.Equal(ExperimentFailureCode.RetryPolicyFailed, completedFailure);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationAbortsWithoutCompletionAndDisposesReverse()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var events = new List<string>();
        var taskStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = CreateDefinition(
            task: async (_, token) =>
            {
                events.Add("task");
                taskStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 1;
            },
            evaluator: null,
            itemScopes:
            [
                CreateOrderedProvider("first", events),
                CreateOrderedProvider("second", events),
            ]);
        var runTask = new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await taskStarted.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(
            [
                "enter:first",
                "enter:second",
                "activate:first",
                "activate:second",
                "task",
                "deactivate:second",
                "deactivate:first",
                "abort:second",
                "abort:first",
                "dispose:second",
                "dispose:first",
            ],
            events);
        Assert.DoesNotContain(
            events,
            value => value.StartsWith("complete:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_CancellationDuringRetryDelayAbortsEnteredScope()
    {
        var timeProvider = new FakeTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var retrySelected = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var aborts = 0;
        var completions = 0;
        var disposals = 0;
        var attempts = 0;
        var provider = new CallbackExperimentItemScopeProvider<int, int>(
            "retry-scope",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () => null,
                        (_, _) =>
                        {
                            Interlocked.Increment(ref completions);
                            return ValueTask.FromResult(SucceededPublication(
                                "retry-scope",
                                isRequired: false));
                        },
                        _ =>
                        {
                            Interlocked.Increment(ref aborts);
                            return ValueTask.CompletedTask;
                        },
                        () =>
                        {
                            Interlocked.Increment(ref disposals);
                            return ValueTask.CompletedTask;
                        });
                return ValueTask.FromResult(scope);
            });
        var runTask = new ExperimentRunner(timeProvider).RunAsync(
            CreateDefinition(
                task: (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("retry later");
                },
                evaluator: null,
                itemScopes: [provider]),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delayProvider: _ =>
                    {
                        retrySelected.SetResult();
                        return TimeSpan.FromHours(1);
                    }),
            },
            cancellation.Token);
        await retrySelected.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, attempts);
        Assert.Equal(1, aborts);
        Assert.Equal(0, completions);
        Assert.Equal(1, disposals);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringCompletionDoesNotAlsoAbortScope()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var completionStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completions = 0;
        var aborts = 0;
        var disposals = 0;
        var provider = new CallbackExperimentItemScopeProvider<int, int>(
            "completion",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () => null,
                        async (_, token) =>
                        {
                            Interlocked.Increment(ref completions);
                            completionStarted.SetResult();
                            await Task.Delay(Timeout.InfiniteTimeSpan, token);
                            return SucceededPublication(
                                "completion",
                                isRequired: false);
                        },
                        _ =>
                        {
                            Interlocked.Increment(ref aborts);
                            return ValueTask.CompletedTask;
                        },
                        () =>
                        {
                            Interlocked.Increment(ref disposals);
                            return ValueTask.CompletedTask;
                        });
                return ValueTask.FromResult(scope);
            });
        var runTask = new ExperimentRunner().RunAsync(
            CreateDefinition(
                task: (_, _) => ValueTask.FromResult(1),
                evaluator: null,
                itemScopes: [provider]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await completionStarted.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, completions);
        Assert.Equal(0, aborts);
        Assert.Equal(1, disposals);
    }

    [Fact]
    public async Task RunAsync_OptionalScopeFailuresDoNotAlterItemQuality()
    {
        var disposals = 0;
        var entryFailure = new CallbackExperimentItemScopeProvider<int, int>(
            "entry",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) => throw new InvalidOperationException("entry failed"));
        var activationFailure = new CallbackExperimentItemScopeProvider<int, int>(
            "activation",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () => throw new InvalidOperationException("activation failed"),
                        (_, _) => ValueTask.FromResult(SucceededPublication(
                            "activation",
                            isRequired: false)),
                        _ => ValueTask.CompletedTask,
                        () =>
                        {
                            Interlocked.Increment(ref disposals);
                            return ValueTask.CompletedTask;
                        });
                return ValueTask.FromResult(scope);
            });

        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                task: (_, _) => ValueTask.FromResult(1),
                evaluator: (_, _) => ValueTask.FromResult(new EvaluationResult()),
                itemScopes: [entryFailure, activationFailure]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal(2, item.Publications.Count);
        Assert.All(
            item.Publications,
            publication =>
            {
                Assert.Equal(ExperimentPublicationOperationStatus.Failed, publication.Status);
                Assert.Equal(ExperimentFailureCode.ItemScopeFailed, publication.Failure!.Code);
                Assert.Equal(ExperimentFailureStage.Publication, publication.Failure.Stage);
            });
        Assert.Equal(1, disposals);
    }

    [Fact]
    public async Task RunAsync_PrerequisiteEntryFailureBlocksTaskAndSkipsLaterScopes()
    {
        var executions = 0;
        var laterEntries = 0;
        var strict = new CallbackExperimentItemScopeProvider<int, int>(
            "strict",
            isRequired: true,
            ExperimentItemScopeFailureMode.ExecutionPrerequisite,
            (_, _) => throw new InvalidOperationException("strict entry failed"));
        var later = new CallbackExperimentItemScopeProvider<int, int>(
            "later",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                Interlocked.Increment(ref laterEntries);
                throw new InvalidOperationException("must not enter");
            });

        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                task: (_, _) =>
                {
                    Interlocked.Increment(ref executions);
                    return ValueTask.FromResult(1);
                },
                evaluator: null,
                itemScopes: [strict, later],
                policies:
                [
                    new ExperimentBinarySuccessPolicy<int, int>(
                        "binary",
                        "passed",
                        requiredSuccessRate: 0.5,
                        minimumSampleCount: 1,
                        confidenceLevel: 0.95),
                ]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.PrerequisiteFailed, item.Status);
        Assert.Empty(item.Attempts);
        Assert.Equal(ExperimentFailureCode.ItemScopePrerequisiteFailed, item.Failure!.Code);
        Assert.Equal(0, executions);
        Assert.Equal(0, laterEntries);
        Assert.Equal(
            [
                ExperimentPublicationOperationStatus.Failed,
                ExperimentPublicationOperationStatus.NotAttempted,
            ],
            item.Publications.Select(publication => publication.Status));
        var policy = Assert.Single(result.Result.PolicyResults);
        Assert.Equal(EvaluationDecision.Inconclusive, policy.Decision);
        Assert.Equal(1, policy.StatisticalEvidence!.ExclusionCount);
        Assert.Equal(
            1,
            policy.StatisticalEvidence.StatusCounts.Single(
                count => count.Status == ExperimentItemStatus.PrerequisiteFailed).Count);
    }

    [Fact]
    public async Task RunAsync_PrerequisiteActivationFailureRollsBackAndCompletesEnteredScopes()
    {
        var events = new List<string>();
        var executions = 0;
        var first = CreateOrderedProvider("first", events);
        var strict = new CallbackExperimentItemScopeProvider<int, int>(
            "strict",
            isRequired: true,
            ExperimentItemScopeFailureMode.ExecutionPrerequisite,
            (_, _) =>
            {
                events.Add("enter:strict");
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () =>
                        {
                            events.Add("activate:strict");
                            throw new InvalidOperationException("activation failed");
                        },
                        (item, _) =>
                        {
                            events.Add($"complete:strict:{item.Status}");
                            return ValueTask.FromResult(SucceededPublication(
                                "strict",
                                isRequired: true));
                        },
                        _ =>
                        {
                            events.Add("abort:strict");
                            return ValueTask.CompletedTask;
                        },
                        () =>
                        {
                            events.Add("dispose:strict");
                            return ValueTask.CompletedTask;
                        });
                return ValueTask.FromResult(scope);
            });

        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                task: (_, _) =>
                {
                    Interlocked.Increment(ref executions);
                    return ValueTask.FromResult(1);
                },
                evaluator: null,
                itemScopes: [first, strict]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.PrerequisiteFailed, item.Status);
        Assert.Equal(0, executions);
        Assert.Equal(
            [
                "enter:first",
                "enter:strict",
                "activate:first",
                "activate:strict",
                "deactivate:first",
                "complete:first",
                $"complete:strict:{ExperimentItemStatus.PrerequisiteFailed}",
                "dispose:strict",
                "dispose:first",
            ],
            events);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            item.Publications[1].Status);
    }

    [Fact]
    public async Task RunAsync_CancellationCleanupTimeoutRemainsCallerCancellation()
    {
        var timeProvider = new FakeTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var taskStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var abortStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverAbort = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverDispose = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new CallbackExperimentItemScopeProvider<int, int>(
            "hanging",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () => null,
                        (_, _) => ValueTask.FromResult(SucceededPublication(
                            "hanging",
                            isRequired: false)),
                        _ =>
                        {
                            abortStarted.SetResult();
                            return new ValueTask(neverAbort.Task);
                        },
                        () =>
                        {
                            disposeStarted.SetResult();
                            return new ValueTask(neverDispose.Task);
                        });
                return ValueTask.FromResult(scope);
            });
        var runTask = new ExperimentRunner(timeProvider).RunAsync(
            CreateDefinition(
                task: async (_, token) =>
                {
                    taskStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return 1;
                },
                evaluator: null,
                itemScopes: [provider]),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                ItemScopeCleanupTimeout = TimeSpan.FromSeconds(5),
            },
            cancellation.Token);
        await taskStarted.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        await abortStarted.Task.WaitAsync(_cancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(disposeStarted.Task.IsCompleted, "Expected disposal to be attempted after abort timeout.");
        Assert.IsType<AggregateException>(exception.InnerException!.InnerException);
    }

    [Fact]
    public async Task RunAsync_DuplicateFeatureTypeFailsLaterOptionalScopeOnly()
    {
        var firstFeature = new ExperimentItemScopeTestFeature("first");
        var secondFeature = new ExperimentItemScopeTestFeature("second");
        var first = CreateFeatureProvider("first", firstFeature);
        var second = CreateFeatureProvider("second", secondFeature);
        string? observedFeature = null;

        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                task: (context, _) =>
                {
                    observedFeature = context.Features
                        .GetRequired<ExperimentItemScopeTestFeature>()
                        .Value;
                    return ValueTask.FromResult(1);
                },
                evaluator: null,
                itemScopes: [first, second]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal("first", observedFeature);
        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, item.Publications[0].Status);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, item.Publications[1].Status);
    }

    [Fact]
    public async Task RunAsync_DisposalFailureDoesNotSuppressReverseCleanupOrQuality()
    {
        var events = new List<string>();
        var first = CreateOrderedProvider("first", events);
        var second = new CallbackExperimentItemScopeProvider<int, int>(
            "second",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                events.Add("enter:second");
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () => null,
                        (_, _) =>
                        {
                            events.Add("complete:second");
                            return ValueTask.FromResult(SucceededPublication(
                                "second",
                                isRequired: false));
                        },
                        _ => ValueTask.CompletedTask,
                        () =>
                        {
                            events.Add("dispose:second");
                            throw new InvalidOperationException("dispose failed");
                        });
                return ValueTask.FromResult(scope);
            });

        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                task: (_, _) => ValueTask.FromResult(1),
                evaluator: null,
                itemScopes: [first, second]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal(
            [
                "enter:first",
                "enter:second",
                "activate:first",
                "deactivate:first",
                "complete:first",
                "complete:second",
                "dispose:second",
                "dispose:first",
            ],
            events);
        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, item.Publications[0].Status);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, item.Publications[1].Status);
        Assert.Equal(
            ExperimentFailureCode.ItemScopeFailed,
            item.Publications[1].Failure!.Code);
    }

    private static CallbackExperimentItemScopeProvider<int, int> CreateOrderedProvider(
        string name,
        ICollection<string> events) =>
        new(
            name,
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                events.Add($"enter:{name}");
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () =>
                        {
                            events.Add($"activate:{name}");
                            return new CallbackDisposable(
                                () => events.Add($"deactivate:{name}"));
                        },
                        (item, _) =>
                        {
                            events.Add($"complete:{name}");
                            return ValueTask.FromResult(SucceededPublication(
                                name,
                                isRequired: false));
                        },
                        _ =>
                        {
                            events.Add($"abort:{name}");
                            return ValueTask.CompletedTask;
                        },
                        () =>
                        {
                            events.Add($"dispose:{name}");
                            return ValueTask.CompletedTask;
                        });
                return ValueTask.FromResult(scope);
            });

    private static CallbackExperimentItemScopeProvider<TCase, TOutput>
        CreateCompletionProvider<TCase, TOutput>(
            string name,
            Action<ExperimentItemResult<TCase, TOutput>> onComplete) =>
        new(
            name,
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<TCase, TOutput> scope =
                    new CallbackExperimentItemScope<TCase, TOutput>(
                        new Dictionary<Type, object>(),
                        () => null,
                        (item, _) =>
                        {
                            onComplete(item);
                            return ValueTask.FromResult(SucceededPublication(
                                name,
                                isRequired: false));
                        },
                        _ => ValueTask.CompletedTask,
                        () => ValueTask.CompletedTask);
                return ValueTask.FromResult(scope);
            });

    private static CallbackExperimentItemScopeProvider<int, int> CreateFeatureProvider(
        string name,
        ExperimentItemScopeTestFeature feature) =>
        new(
            name,
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>
                        {
                            [typeof(ExperimentItemScopeTestFeature)] = feature,
                        },
                        () => null,
                        (_, _) => ValueTask.FromResult(SucceededPublication(
                            name,
                            isRequired: false)),
                        _ => ValueTask.CompletedTask,
                        () => ValueTask.CompletedTask);
                return ValueTask.FromResult(scope);
            });

    private static ExperimentItemPublicationResult SucceededPublication(
        string name,
        bool isRequired,
        params ExperimentItemCorrelation[] correlations) =>
        new()
        {
            Name = name,
            IsRequired = isRequired,
            Status = ExperimentPublicationOperationStatus.Succeeded,
            Correlations = Array.AsReadOnly(correlations),
        };

    private static ExperimentDefinition<int, int> CreateDefinition(
        ExperimentTask<int, int> task,
        ExperimentItemEvaluator<int, int>? evaluator,
        IReadOnlyList<IExperimentItemScopeProvider<int, int>> itemScopes,
        IReadOnlyList<IExperimentRunPolicy<int, int>>? policies = null) =>
        new()
        {
            Name = "item-scopes",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
            Task = task,
            ItemEvaluator = evaluator,
            ItemScopes = itemScopes,
            Policies = policies ?? [],
        };

    private async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private async Task AdvanceUntilAsync(
        FakeTimeProvider timeProvider,
        Func<bool> predicate,
        TimeSpan increment)
    {
        while (!predicate())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(increment);
            await Task.Yield();
        }
    }
}
