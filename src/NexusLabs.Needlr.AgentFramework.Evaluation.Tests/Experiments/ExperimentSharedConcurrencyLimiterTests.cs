using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentSharedConcurrencyLimiterTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_TwoRunsSharingLimiter_RespectCombinedLimit()
    {
        await using var limiter = new RecordingExperimentConcurrencyLimiter(1);
        var activeTasks = 0;
        var maximumActiveTasks = 0;
        var entered = 0;
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = new ExperimentDefinition<int, int>
        {
            Name = "shared-limit",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
            Task = async (_, cancellationToken) =>
            {
                var active = Interlocked.Increment(ref activeTasks);
                UpdateMaximum(ref maximumActiveTasks, active);
                var order = Interlocked.Increment(ref entered);
                try
                {
                    await (order == 1 ? releaseFirst.Task : releaseSecond.Task)
                        .WaitAsync(cancellationToken);
                    return order;
                }
                finally
                {
                    Interlocked.Decrement(ref activeTasks);
                }
            },
        };
        var runner = new ExperimentRunner();
        var firstRun = runner.RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                SharedLimiter = limiter,
            },
            _cancellationToken);
        await WaitUntilAsync(() => Volatile.Read(ref entered) == 1);

        var secondRun = runner.RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-2",
                MaxConcurrency = 1,
                SharedLimiter = limiter,
            },
            _cancellationToken);
        await WaitUntilAsync(() => limiter.AcquisitionCount == 2);

        Assert.Equal(1, entered);
        Assert.Equal(1, activeTasks);
        Assert.Equal(1, limiter.ActiveLeaseCount);
        releaseFirst.SetResult();
        await WaitUntilAsync(() => Volatile.Read(ref entered) == 2);

        Assert.Equal(1, maximumActiveTasks);
        Assert.Equal(1, limiter.MaximumActiveLeaseCount);
        releaseSecond.SetResult();
        await Task.WhenAll(firstRun, secondRun);

        Assert.Equal(0, activeTasks);
        Assert.Equal(0, limiter.ActiveLeaseCount);
        Assert.Equal(0, limiter.DisposeCount);
    }

    [Fact]
    public async Task ExperimentConcurrencyLimiter_LeaseReleaseAllowsNextAcquisition()
    {
        await using var limiter = new ExperimentConcurrencyLimiter(1);
        await using var firstLease = await limiter.AcquireAsync(_cancellationToken);
        var secondAcquired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTask = AcquireSecondAsync();

        await Task.Yield();
        Assert.False(
            secondAcquired.Task.IsCompleted,
            "Expected the second acquisition to wait while the first lease is active.");
        await firstLease.DisposeAsync();
        await secondAcquired.Task.WaitAsync(_cancellationToken);
        await secondTask;

        async Task AcquireSecondAsync()
        {
            await using var secondLease = await limiter.AcquireAsync(_cancellationToken);
            secondAcquired.SetResult();
        }
    }

    private async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (candidate <= current
                || Interlocked.CompareExchange(ref maximum, candidate, current) == current)
            {
                return;
            }
        }
    }
}
