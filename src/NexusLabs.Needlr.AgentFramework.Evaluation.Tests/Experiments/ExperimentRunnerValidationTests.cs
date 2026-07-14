using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerValidationTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_InvalidDefinitionAndOptions_StartNoItems()
    {
        var executions = 0;
        var validCase = new ExperimentCase<int>
        {
            Id = "case-1",
            Value = 1,
        };
        var source = new LocalExperimentCaseSource<int>("local", [validCase]);
        var validDefinition = new ExperimentDefinition<int, int>
        {
            Name = "validation",
            CaseSource = source,
            Task = (_, _) =>
            {
                Interlocked.Increment(ref executions);
                return ValueTask.FromResult(1);
            },
        };
        var runner = new ExperimentRunner();

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = " ",
                CaseSource = source,
                Task = validDefinition.Task,
            },
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(
            validDefinition,
            new ExperimentRunOptions { RunId = " ", MaxConcurrency = 1 },
            _cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(
            validDefinition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 0 },
            _cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(
            validDefinition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                AttemptTimeout = TimeSpan.Zero,
            },
            _cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(
            validDefinition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                AttemptTimeout = TimeSpan.MaxValue,
            },
            _cancellationToken));

        Assert.Equal(0, executions);
    }

    [Fact]
    public async Task RunAsync_InvalidCases_StartNoItems()
    {
        var executions = 0;
        var runner = new ExperimentRunner();

        await AssertValidationFailureAsync(
            [
                new ExperimentCase<int> { Id = " ", Value = 1 },
            ]);
        await AssertValidationFailureAsync(
            [
                new ExperimentCase<int> { Id = "duplicate", Value = 1 },
                new ExperimentCase<int> { Id = "duplicate", Value = 2 },
            ]);
        await AssertValidationFailureAsync(
            [
                new ExperimentCase<int> { Id = "case-1", Value = 1, TrialCount = 0 },
            ]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(
            CreateDefinition(
            [
                new ExperimentCase<int>
                {
                    Id = "case-1",
                    Value = 1,
                    TrialCount = int.MaxValue,
                },
                new ExperimentCase<int>
                {
                    Id = "case-2",
                    Value = 2,
                    TrialCount = 1,
                },
            ]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken));

        Assert.Equal(0, executions);

        async Task AssertValidationFailureAsync(
            IReadOnlyList<ExperimentCase<int>> cases)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => runner.RunAsync(
                CreateDefinition(cases),
                new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
                _cancellationToken));
        }

        ExperimentDefinition<int, int> CreateDefinition(
            IReadOnlyList<ExperimentCase<int>> cases) =>
            new()
            {
                Name = "validation",
                CaseSource = new LocalExperimentCaseSource<int>("local", cases),
                Task = (_, _) =>
                {
                    Interlocked.Increment(ref executions);
                    return ValueTask.FromResult(1);
                },
            };
    }
}
