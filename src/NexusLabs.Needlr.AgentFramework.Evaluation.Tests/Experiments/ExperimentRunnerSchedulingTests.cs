using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerSchedulingTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_Saturation_NeverExceedsMaxConcurrency()
    {
        const int maxConcurrency = 3;
        var active = 0;
        var maximumActive = 0;
        var entered = 0;
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = CreateDefinition(
            Enumerable.Range(0, 20)
                .Select(index => new ExperimentCase<int>
                {
                    Id = $"case-{index}",
                    Value = index,
                })
                .ToArray(),
            async (context, cancellationToken) =>
            {
                var current = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximumActive, current);
                Interlocked.Increment(ref entered);
                try
                {
                    await release.Task.WaitAsync(cancellationToken);
                    return context.Case.Value;
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            });
        var runner = new ExperimentRunner();

        var runTask = runner.RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = maxConcurrency,
            },
            _cancellationToken);
        await WaitUntilAsync(() => Volatile.Read(ref entered) == maxConcurrency);

        Assert.Equal(maxConcurrency, maximumActive);
        Assert.Equal(maxConcurrency, active);
        release.SetResult();

        var result = await runTask;

        Assert.Equal(20, result.Items.Count);
        Assert.Equal(maxConcurrency, result.WorkerCount);
        Assert.Equal(0, active);
        Assert.Equal(
            Enumerable.Range(0, 20),
            result.Items.Select(item => item.Sequence));
    }

    [Fact]
    public async Task RunAsync_OutOfOrderCompletion_ReturnsSourceAndTrialOrder()
    {
        var completions = Enumerable.Range(0, 6)
            .Select(_ => new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        var entered = 0;
        var cases = new[]
        {
            new ExperimentCase<string>
            {
                Id = "alpha",
                Value = "A",
                TrialCount = 2,
            },
            new ExperimentCase<string>
            {
                Id = "beta",
                Value = "B",
                TrialCount = 1,
            },
            new ExperimentCase<string>
            {
                Id = "gamma",
                Value = "C",
                TrialCount = 3,
            },
        };
        var definition = CreateDefinition(
            cases,
            async (context, cancellationToken) =>
            {
                Interlocked.Increment(ref entered);
                await completions[context.Sequence].Task.WaitAsync(cancellationToken);
                return $"{context.Case.Id}:{context.TrialIndex}";
            });
        var runner = new ExperimentRunner();

        var runTask = runner.RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 6,
            },
            _cancellationToken);
        await WaitUntilAsync(() => Volatile.Read(ref entered) == 6);
        for (var index = completions.Length - 1; index >= 0; index--)
        {
            completions[index].SetResult();
        }

        var result = await runTask;

        Assert.Equal(
            new string?[]
            {
                "alpha:1",
                "alpha:2",
                "beta:1",
                "gamma:1",
                "gamma:2",
                "gamma:3",
            },
            result.Items.Select(item => item.Output).ToArray());
        Assert.Equal(
            ["alpha", "alpha", "beta", "gamma", "gamma", "gamma"],
            result.Items.Select(item => item.Case.Id).ToArray());
        Assert.Equal([1, 2, 1, 1, 2, 3], result.Items.Select(item => item.TrialIndex).ToArray());
    }

    [Fact]
    public async Task RunAsync_MaterializationCopiesSourceCollectionAndCaseMetadata()
    {
        var tags = new List<string> { "original" };
        var sourceCase = new ExperimentCase<int>
        {
            Id = "case-1",
            Value = 7,
            TrialCount = 1,
            Tags = tags,
        };
        var cases = new List<ExperimentCase<int>> { sourceCase };
        var source = new LocalExperimentCaseSource<int>("local", cases);
        cases.Add(new ExperimentCase<int> { Id = "late-case", Value = 8 });
        var entered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ExperimentRunner();
        var runTask = runner.RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "snapshot",
                CaseSource = source,
                Task = async (context, _) =>
                {
                    entered.SetResult();
                    await release.Task;
                    return context.Case.Value;
                },
            },
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);
        await entered.Task.WaitAsync(_cancellationToken);

        tags.Add("late-tag");
        release.SetResult();
        var result = await runTask;

        var item = Assert.Single(result.Items);
        Assert.Equal("case-1", item.Case.Id);
        Assert.Equal(["original"], item.Case.Tags);
    }

    [Fact]
    public async Task RunAsync_EmptySource_ReturnsEmptyResultWithoutWorkers()
    {
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "empty",
                CaseSource = new LocalExperimentCaseSource<int>("local", []),
                Task = (_, _) => ValueTask.FromResult(1),
            },
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 4 },
            _cancellationToken);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.WorkerCount);
    }

    private static ExperimentDefinition<TCase, TOutput> CreateDefinition<TCase, TOutput>(
        IReadOnlyList<ExperimentCase<TCase>> cases,
        ExperimentTask<TCase, TOutput> task) =>
        new()
        {
            Name = "scheduling",
            CaseSource = new LocalExperimentCaseSource<TCase>("local", cases),
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
