using Microsoft.Extensions.Time.Testing;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerCancellationTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_PreCanceledCallerToken_PropagatesExactTokenAndStartsNoItems()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        cancellation.Cancel();
        var executions = 0;
        var runner = new ExperimentRunner();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(
                CreateDefinition(
                    (_, _) =>
                    {
                        Interlocked.Increment(ref executions);
                        return ValueTask.FromResult(1);
                    }),
                new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, executions);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationDuringAttempts_PropagatesExactToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var entered = 0;
        var runner = new ExperimentRunner();
        var runTask = runner.RunAsync(
            CreateDefinition(
                async (_, token) =>
                {
                    Interlocked.Increment(ref entered);
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return 1;
                },
                caseCount: 4),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 2 },
            cancellation.Token);
        await WaitUntilAsync(() => Volatile.Read(ref entered) == 2);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(2, entered);
    }

    [Fact]
    public async Task RunAsync_AttemptDeadline_ClassifiesTimedOut()
    {
        var timeProvider = new FakeTimeProvider();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ExperimentRunner(timeProvider);
        var runTask = runner.RunAsync(
            CreateDefinition(
                async (_, token) =>
                {
                    entered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, token);
                    return 1;
                }),
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

        var item = Assert.Single(result.Items);
        Assert.Equal(ExperimentItemStatus.TimedOut, item.Status);
        Assert.Equal(ExperimentAttemptStatus.TimedOut, Assert.Single(item.Attempts).Status);
        Assert.Equal(ExperimentFailureCode.AttemptTimedOut, item.Failure!.Code);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationWinsOverSimultaneousTimeout()
    {
        var timeProvider = new FakeTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ExperimentRunner(timeProvider);
        var runTask = runner.RunAsync(
            CreateDefinition(
                async (_, token) =>
                {
                    entered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, token);
                    return 1;
                }),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                AttemptTimeout = TimeSpan.FromSeconds(5),
            },
            cancellation.Token);
        await entered.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task RunAsync_TaskThatReturnsAfterDeadline_IsStillTimedOut()
    {
        var timeProvider = new FakeTimeProvider();
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ExperimentRunner(timeProvider);
        var runTask = runner.RunAsync(
            CreateDefinition(
                async (_, _) =>
                {
                    entered.SetResult();
                    await release.Task;
                    return 42;
                }),
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                AttemptTimeout = TimeSpan.FromSeconds(5),
            },
            _cancellationToken);
        await entered.Task.WaitAsync(_cancellationToken);

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        release.SetResult();
        var result = await runTask;

        var item = Assert.Single(result.Items);
        Assert.Equal(ExperimentItemStatus.TimedOut, item.Status);
        Assert.False(item.HasOutput, "Expected output returned after the deadline to be discarded.");
    }

    private static ExperimentDefinition<int, int> CreateDefinition(
        ExperimentTask<int, int> task,
        int caseCount = 1) =>
        new()
        {
            Name = "cancellation",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                Enumerable.Range(0, caseCount).Select(index =>
                    new ExperimentCase<int>
                    {
                        Id = $"case-{index}",
                        Value = index,
                    })),
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
}
