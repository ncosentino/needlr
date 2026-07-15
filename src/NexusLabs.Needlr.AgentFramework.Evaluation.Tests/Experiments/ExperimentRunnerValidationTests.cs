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
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(
            validDefinition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
                ItemScopeCleanupTimeout = TimeSpan.Zero,
            },
            _cancellationToken));

        Assert.Equal(0, executions);
    }

    [Fact]
    public async Task RunAsync_DuplicateOrInvalidItemScopes_StartNoItems()
    {
        var executions = 0;
        var runner = new ExperimentRunner();
        var provider = new CallbackExperimentItemScopeProvider<int, int>(
            "duplicate",
            isRequired: false,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) => throw new InvalidOperationException("must not enter"));
        var invalidMode = new CallbackExperimentItemScopeProvider<int, int>(
            "invalid",
            isRequired: false,
            (ExperimentItemScopeFailureMode)int.MaxValue,
            (_, _) => throw new InvalidOperationException("must not enter"));
        ExperimentDefinition<int, int> CreateDefinition(
            IReadOnlyList<IExperimentItemScopeProvider<int, int>> itemScopes) =>
            new()
            {
                Name = "validation",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
                Task = (_, _) =>
                {
                    Interlocked.Increment(ref executions);
                    return ValueTask.FromResult(1);
                },
                ItemScopes = itemScopes,
            };

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(
            CreateDefinition([provider, provider]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(
            CreateDefinition([invalidMode]),
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
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

    [Fact]
    public async Task RunAsync_DuplicateRunEvaluatorOrPolicyNames_StartNoItems()
    {
            var executions = 0;
            var runner = new ExperimentRunner();
            var evaluator = new ExperimentRunEvaluator<int, int>(
                "duplicate",
                (_, _) => ValueTask.FromResult(
                    new Microsoft.Extensions.AI.Evaluation.EvaluationResult()));
            var policy = new CallbackExperimentPolicy<int, int>("duplicate", () => { });

            await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(
                CreateDefinition(
                    runEvaluators: [evaluator, evaluator],
                    policies: []),
                new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
                _cancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(
                CreateDefinition(
                    runEvaluators: [],
                    policies: [policy, policy]),
                new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
                _cancellationToken));

            Assert.Equal(0, executions);

            ExperimentDefinition<int, int> CreateDefinition(
                IReadOnlyList<IExperimentRunEvaluator<int, int>> runEvaluators,
                IReadOnlyList<IExperimentRunPolicy<int, int>> policies) =>
                new()
                {
                    Name = "validation",
                    CaseSource = new LocalExperimentCaseSource<int>(
                        "local",
                        [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
                    Task = (_, _) =>
                    {
                        Interlocked.Increment(ref executions);
                        return ValueTask.FromResult(1);
                    },
                    RunEvaluators = runEvaluators,
                    Policies = policies,
                };
    }
}
