using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerRunEvaluationTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_RunEvaluator_ReceivesEveryOrderedItemStatus()
    {
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        IReadOnlyList<ExperimentItemStatus>? observedStatuses = null;
        var timeoutEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = CreateMixedDefinition(
            timeProvider,
            timeoutEntered,
            [
                new ExperimentRunEvaluator<int, int>(
                    "status-counts",
                    (context, _) =>
                    {
                        observedStatuses = context.Items
                            .Select(item => item.Status)
                            .ToArray();
                        return ValueTask.FromResult(new EvaluationResult(
                            new NumericMetric("item_count", context.Items.Count)));
                    }),
            ]);
        var runTask = new ExperimentRunner(timeProvider).RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 5,
                AttemptTimeout = TimeSpan.FromSeconds(5),
            },
            _cancellationToken);
        await timeoutEntered.Task.WaitAsync(_cancellationToken);
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        var result = await runTask;

        Assert.Equal(
            [
                ExperimentItemStatus.Succeeded,
                ExperimentItemStatus.ExecutionFailed,
                ExperimentItemStatus.TimedOut,
                ExperimentItemStatus.Canceled,
                ExperimentItemStatus.EvaluationFailed,
                ExperimentItemStatus.PrerequisiteFailed,
            ],
            observedStatuses);
        var runEvaluation = Assert.Single(result.Result.RunEvaluations);
        Assert.Equal("status-counts", runEvaluation.Name);
        Assert.Equal(ExperimentRunEvaluationStatus.Succeeded, runEvaluation.Status);
        Assert.Equal("item_count", Assert.Single(runEvaluation.Metrics).Name);
    }

    [Fact]
    public async Task RunAsync_RunEvaluatorFailure_IsolatedAndLaterEvaluatorStillRuns()
    {
        var laterEvaluatorCalls = 0;
        var definition = new ExperimentDefinition<int, int>
        {
            Name = "run-evaluation",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
            Task = (_, _) => ValueTask.FromResult(1),
            RunEvaluators =
            [
                new ExperimentRunEvaluator<int, int>(
                    "failure",
                    (_, _) => throw new InvalidOperationException("run evaluation failed")),
                new ExperimentRunEvaluator<int, int>(
                    "success",
                    (_, _) =>
                    {
                        Interlocked.Increment(ref laterEvaluatorCalls);
                        return ValueTask.FromResult(new EvaluationResult(
                            new BooleanMetric("healthy", true)));
                    }),
            ],
        };

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(1, laterEvaluatorCalls);
        Assert.Equal(2, result.Result.RunEvaluations.Count);
        Assert.Equal(
            [ExperimentRunEvaluationStatus.Failed, ExperimentRunEvaluationStatus.Succeeded],
            result.Result.RunEvaluations.Select(evaluation => evaluation.Status));
        Assert.Equal(
            ExperimentFailureCode.RunEvaluationFailed,
            result.Result.RunEvaluations[0].Failure!.Code);
        Assert.Equal(
            ExperimentFailureStage.RunEvaluation,
            result.Result.RunEvaluations[0].Failure!.Stage);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationDuringRunEvaluation_PropagatesExactToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var evaluatorStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = new ExperimentDefinition<int, int>
        {
            Name = "run-evaluation-cancellation",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
            Task = (_, _) => ValueTask.FromResult(1),
            RunEvaluators =
            [
                new ExperimentRunEvaluator<int, int>(
                    "blocking",
                    async (_, token) =>
                    {
                        evaluatorStarted.SetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                        return new EvaluationResult();
                    }),
            ],
        };
        var runTask = new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await evaluatorStarted.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static ExperimentDefinition<int, int> CreateMixedDefinition(
        TimeProvider timeProvider,
        TaskCompletionSource timeoutEntered,
        IReadOnlyList<IExperimentRunEvaluator<int, int>> runEvaluators)
    {
        return new ExperimentDefinition<int, int>
        {
            Name = "all-statuses",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                Enumerable.Range(0, 6).Select(index =>
                    new ExperimentCase<int>
                    {
                        Id = $"case-{index}",
                        Value = index,
                    })),
            Task = async (context, cancellationToken) => context.Case.Value switch
            {
                1 => throw new InvalidOperationException("execution failed"),
                2 => await WaitForTimeoutAsync(cancellationToken),
                3 => throw new OperationCanceledException("task canceled"),
                _ => context.Case.Value,
            },
            ItemEvaluator = (context, _) => context.Case.Value == 4
                ? throw new InvalidOperationException("evaluation failed")
                : ValueTask.FromResult(new EvaluationResult()),
            ItemScopes =
            [
                new CallbackExperimentItemScopeProvider<int, int>(
                    "prerequisite",
                    isRequired: false,
                    ExperimentItemScopeFailureMode.ExecutionPrerequisite,
                    (context, _) =>
                    {
                        if (context.Case.Value == 5)
                        {
                            throw new InvalidOperationException("prerequisite failed");
                        }

                        IExperimentItemScope<int, int> scope =
                            new CallbackExperimentItemScope<int, int>(
                                new Dictionary<Type, object>(),
                                () => null,
                                (_, _) => ValueTask.FromResult(
                                    ExperimentItemPublicationOperationResult.Succeeded([])),
                                _ => ValueTask.CompletedTask,
                                () => ValueTask.CompletedTask);
                        return ValueTask.FromResult(scope);
                    }),
            ],
            RunEvaluators = runEvaluators,
        };

        async Task<int> WaitForTimeoutAsync(CancellationToken cancellationToken)
        {
            timeoutEntered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, cancellationToken);
            return 2;
        }
    }
}
