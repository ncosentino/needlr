using Microsoft.Extensions.Time.Testing;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerRetryTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_SelectedFailure_RetriesUntilSuccessAndRecordsDelays()
    {
        var timeProvider = new FakeTimeProvider();
        var attempts = 0;
        var runner = new ExperimentRunner(timeProvider);
        var runTask = runner.RunAsync(
            CreateDefinition(
                (context, _) =>
                {
                    var attempt = Interlocked.Increment(ref attempts);
                    return attempt < 3
                        ? throw new InvalidOperationException($"failure-{attempt}")
                        : ValueTask.FromResult(context.AttemptNumber);
                }),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 3,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.FromSeconds(5)),
            },
            _cancellationToken);
        await WaitUntilAsync(() => Volatile.Read(ref attempts) == 1);

        await AdvanceUntilAsync(
            timeProvider,
            () => Volatile.Read(ref attempts) == 2,
            TimeSpan.FromSeconds(5));
        await AdvanceUntilAsync(
            timeProvider,
            () => Volatile.Read(ref attempts) == 3,
            TimeSpan.FromSeconds(5));

        var result = await runTask;

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal(3, item.Output);
        Assert.Equal(
            [
                ExperimentAttemptStatus.Failed,
                ExperimentAttemptStatus.Failed,
                ExperimentAttemptStatus.Succeeded,
            ],
            item.Attempts.Select(attempt => attempt.Status).ToArray());
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), null],
            item.Attempts.Select(attempt => attempt.DelayBeforeNextAttempt).ToArray());
        Assert.Equal([1, 2, 3], item.Attempts.Select(attempt => attempt.AttemptNumber).ToArray());
        Assert.True(
            item.Attempts[0].Failure!.IsRetryable,
            "Expected an attempt selected for retry to be marked retryable.");
        Assert.False(
            item.Attempts[2].Failure?.IsRetryable ?? false,
            "Expected the successful terminal attempt not to be marked retryable.");
    }

    [Fact]
    public async Task RunAsync_ExhaustedRetryBudget_RetainsEveryAttempt()
    {
        var attempts = 0;
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            CreateDefinition(
                (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("always fails");
                }),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 3,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(3, attempts);
        Assert.Equal(ExperimentItemStatus.ExecutionFailed, item.Status);
        Assert.Equal(3, item.Attempts.Count);
        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.Zero, null],
            item.Attempts.Select(x => x.DelayBeforeNextAttempt));
        Assert.False(
            item.Attempts[^1].Failure!.IsRetryable,
            "Expected an exhausted terminal failure not to claim another retry.");
    }

    [Fact]
    public async Task RunAsync_SelectedTimeout_RetriesAsAnotherAttempt()
    {
        var timeProvider = new FakeTimeProvider();
        var firstAttemptEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ExperimentRunner(timeProvider);
        var runTask = runner.RunAsync(
            CreateDefinition(
                async (context, cancellationToken) =>
                {
                    if (context.AttemptNumber == 1)
                    {
                        firstAttemptEntered.SetResult();
                        await Task.Delay(
                            Timeout.InfiniteTimeSpan,
                            timeProvider,
                            cancellationToken);
                    }

                    return context.AttemptNumber;
                }),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                AttemptTimeout = TimeSpan.FromSeconds(5),
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.Timeout,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);
        await firstAttemptEntered.Task.WaitAsync(_cancellationToken);

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        var result = await runTask;

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal(2, item.Output);
        Assert.Equal(
            [ExperimentAttemptStatus.TimedOut, ExperimentAttemptStatus.Succeeded],
            item.Attempts.Select(attempt => attempt.Status));
    }

    [Fact]
    public async Task RunAsync_SelectedTaskCancellation_RetriesAsAnotherAttempt()
    {
        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                (context, _) => context.AttemptNumber == 1
                    ? throw new OperationCanceledException("task canceled")
                    : ValueTask.FromResult(context.AttemptNumber)),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.TaskCancellation,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal(2, item.Output);
        Assert.Equal(
            [ExperimentAttemptStatus.Canceled, ExperimentAttemptStatus.Succeeded],
            item.Attempts.Select(attempt => attempt.Status));
    }

    [Fact]
    public async Task RunAsync_DelayedRetry_ReleasesWorkerAndSharedLimiter()
    {
        var timeProvider = new FakeTimeProvider();
        await using var limiter = new RecordingExperimentConcurrencyLimiter(1);
        var retryAttempts = 0;
        var otherItemEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ExperimentRunner(timeProvider);
        var definition = new ExperimentDefinition<string, string>
        {
            Name = "retry-scheduling",
            CaseSource = new LocalExperimentCaseSource<string>(
                "local",
                [
                    new ExperimentCase<string> { Id = "retry", Value = "retry" },
                    new ExperimentCase<string> { Id = "other", Value = "other" },
                ]),
            Task = (context, _) =>
            {
                if (context.Case.Id == "retry")
                {
                    var attempt = Interlocked.Increment(ref retryAttempts);
                    return attempt == 1
                        ? throw new InvalidOperationException("retry me")
                        : ValueTask.FromResult("retried");
                }

                otherItemEntered.SetResult();
                return ValueTask.FromResult("other");
            },
        };
        var runTask = runner.RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                SharedLimiter = limiter,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.FromMinutes(1)),
            },
            _cancellationToken);

        await otherItemEntered.Task.WaitAsync(_cancellationToken);

        Assert.Equal(1, retryAttempts);
        Assert.Equal(2, limiter.AcquisitionCount);
        Assert.Equal(0, limiter.ActiveLeaseCount);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        var result = await runTask;

        Assert.Equal(2, retryAttempts);
        Assert.Equal(3, limiter.AcquisitionCount);
        Assert.Equal(1, limiter.MaximumActiveLeaseCount);
        Assert.Equal(0, limiter.ActiveLeaseCount);
        Assert.Equal(2, result.Result.Items.Count);
    }

    [Fact]
    public async Task RunAsync_RetriesRemainAttemptsWithinOriginalTrials()
    {
        var attemptsByTrial = new Dictionary<int, int>();
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "trials",
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
                Task = (context, _) =>
                {
                    attemptsByTrial.TryGetValue(context.TrialIndex, out var attemptCount);
                    attemptsByTrial[context.TrialIndex] = attemptCount + 1;
                    return context.AttemptNumber == 1
                        ? throw new InvalidOperationException("first attempt fails")
                        : ValueTask.FromResult(context.TrialIndex);
                },
            },
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);

        Assert.Equal(2, result.Result.Items.Count);
        Assert.Equal([1, 2], result.Result.Items.Select(item => item.TrialIndex));
        Assert.All(result.Result.Items, item => Assert.Equal(2, item.Attempts.Count));
        Assert.Equal(2, attemptsByTrial[1]);
        Assert.Equal(2, attemptsByTrial[2]);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationDuringRetryDelay_PropagatesExactToken()
    {
        var timeProvider = new FakeTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var retrySelected = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        var retryPolicy = new ExperimentRetryPolicy(
            maxAttempts: 2,
            retryOn: ExperimentRetryableOutcome.ExecutionFailure,
            delayProvider: _ =>
            {
                retrySelected.SetResult();
                return TimeSpan.FromHours(1);
            });
        var runTask = new ExperimentRunner(timeProvider).RunAsync(
            CreateDefinition(
                (_, _) =>
                {
                    Interlocked.Increment(ref executions);
                    throw new InvalidOperationException("retry later");
                }),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = retryPolicy,
            },
            cancellation.Token);
        await retrySelected.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, executions);
    }

    [Fact]
    public async Task RunAsync_EvaluatorFailure_WithRetryPolicy_DoesNotReplayExecution()
    {
        var executions = 0;
        var evaluatorCalls = 0;
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "evaluator",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
                Task = (_, _) =>
                {
                    Interlocked.Increment(ref executions);
                    return ValueTask.FromResult(42);
                },
                ItemEvaluator = (_, _) =>
                {
                    Interlocked.Increment(ref evaluatorCalls);
                    throw new InvalidOperationException("evaluation failed");
                },
            },
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 3,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure
                        | ExperimentRetryableOutcome.Timeout
                        | ExperimentRetryableOutcome.TaskCancellation,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(1, executions);
        Assert.Equal(1, evaluatorCalls);
        Assert.Equal(ExperimentItemStatus.EvaluationFailed, item.Status);
        Assert.Single(item.Attempts);
    }

    [Fact]
    public async Task RunAsync_RetryPolicyFailure_BecomesStructuredTerminalItemFailure()
    {
        var result = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                (_, _) => throw new InvalidOperationException("execution failed")),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                RetryPolicy = new ThrowingExperimentRetryPolicy(),
            },
            _cancellationToken);

        var item = Assert.Single(result.Result.Items);
        Assert.Equal(ExperimentItemStatus.ExecutionFailed, item.Status);
        Assert.Single(item.Attempts);
        Assert.Equal(ExperimentFailureCode.RetryPolicyFailed, item.Failure!.Code);
        Assert.Equal(ExperimentFailureStage.Policy, item.Failure.Stage);
        Assert.Equal("retry policy failed", item.Failure.Message);
    }

    private static ExperimentDefinition<int, int> CreateDefinition(
        ExperimentTask<int, int> task) =>
        new()
        {
            Name = "retry",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
            Task = task,
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
