using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentPolicyTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_DeterministicThresholdPolicies_ProduceOrderedDecisions()
    {
        var thresholds = new EvaluationThresholdEvaluator()
            .RequireNumericMin("success_rate", 0.8)
            .RequireBoolean("stable", expected: true);
        var definition = CreateDefinition(
            trialCount: 1,
            (_, _) => ValueTask.FromResult(new EvaluationResult(
                new BooleanMetric("passed", true))),
            runEvaluators:
            [
                new ExperimentRunEvaluator<int, int>(
                    "aggregate",
                    (_, _) => ValueTask.FromResult(new EvaluationResult(
                        new NumericMetric("success_rate", 0.9),
                        new BooleanMetric("stable", true)))),
            ],
            policies:
            [
                new ExperimentRunEvaluationThresholdPolicy<int, int>(
                    "passing",
                    "aggregate",
                    thresholds),
                new ExperimentRunEvaluationThresholdPolicy<int, int>(
                    "missing",
                    "aggregate",
                    new EvaluationThresholdEvaluator().RequireBoolean("missing_metric", true)),
                new ExperimentRunEvaluationThresholdPolicy<int, int>(
                    "failing",
                    "aggregate",
                    new EvaluationThresholdEvaluator().RequireNumericMax("success_rate", 0.5)),
            ]);

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(
            ["passing", "missing", "failing"],
            result.PolicyResults.Select(policy => policy.Name));
        Assert.Equal(
            [EvaluationDecision.Passed, EvaluationDecision.Inconclusive, EvaluationDecision.Failed],
            result.PolicyResults.Select(policy => policy.Decision));
        Assert.Equal(ExperimentRunDecision.Failed, result.Decision);
        Assert.All(
            result.PolicyResults,
            policy => Assert.Equal(ExperimentPolicyKind.Deterministic, policy.Kind));
    }

    [Fact]
    public async Task RunAsync_OptionalFailedPolicy_DoesNotFailRequiredPassingDecision()
    {
        var definition = CreateDefinition(
            trialCount: 1,
            (_, _) => ValueTask.FromResult(new EvaluationResult(
                new BooleanMetric("passed", true))),
            policies:
            [
                new ExperimentBinarySuccessPolicy<int, int>(
                    "required",
                    "passed",
                    requiredSuccessRate: 0,
                    minimumSampleCount: 1,
                    confidenceLevel: 0.95,
                    isRequired: true),
                new ExperimentBinarySuccessPolicy<int, int>(
                    "optional",
                    "passed",
                    requiredSuccessRate: 1,
                    minimumSampleCount: 1,
                    confidenceLevel: 0.95,
                    isRequired: false),
            ]);

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(EvaluationDecision.Passed, result.PolicyResults[0].Decision);
        Assert.Equal(EvaluationDecision.Inconclusive, result.PolicyResults[1].Decision);
        Assert.Equal(ExperimentRunDecision.Passed, result.Decision);
    }

    [Fact]
    public async Task RunAsync_BinaryStatisticalPolicies_ProducePassedFailedAndInconclusive()
    {
        var passing = await RunBinaryPolicyAsync(
            "passing",
            trialCount: 100,
            metricValue: true,
            requiredSuccessRate: 0.8);
        var failing = await RunBinaryPolicyAsync(
            "failing",
            trialCount: 100,
            metricValue: false,
            requiredSuccessRate: 0.8);
        var inconclusive = await RunBinaryPolicyAsync(
            "inconclusive",
            trialCount: 1,
            metricValue: true,
            requiredSuccessRate: 0.8);

        Assert.Equal(EvaluationDecision.Passed, passing.Decision);
        Assert.Equal(EvaluationDecision.Failed, failing.Decision);
        Assert.Equal(EvaluationDecision.Inconclusive, inconclusive.Decision);

        var evidence = passing.StatisticalEvidence!;
        Assert.Equal(100, evidence.TotalTrialCount);
        Assert.Equal(100, evidence.AttemptCount);
        Assert.Equal(100, evidence.SampleCount);
        Assert.Equal(100, evidence.SuccessCount);
        Assert.Equal(0, evidence.FailureCount);
        Assert.Equal(0, evidence.ExclusionCount);
        Assert.Equal(1, evidence.Estimate);
        Assert.Equal(0.95, evidence.ConfidenceLevel);
        Assert.Equal(0.9736572793, evidence.OneSidedLowerBound!.Value, precision: 10);
    }

    [Fact]
    public async Task RunAsync_BinaryPolicy_UnknownSamplesAreInconclusiveOrPessimisticFailures()
    {
        var definition = new ExperimentDefinition<int, int>
        {
            Name = "unknown-samples",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [
                    new ExperimentCase<int> { Id = "valid", Value = 0 },
                    new ExperimentCase<int> { Id = "missing", Value = 1 },
                    new ExperimentCase<int> { Id = "execution-failure", Value = 2 },
                ]),
            Task = (context, _) => context.Case.Value == 2
                ? throw new InvalidOperationException("failed")
                : ValueTask.FromResult(context.Case.Value),
            ItemEvaluator = (context, _) => ValueTask.FromResult(
                context.Case.Value == 0
                    ? new EvaluationResult(new BooleanMetric("passed", true))
                    : new EvaluationResult()),
            Policies =
            [
                new ExperimentBinarySuccessPolicy<int, int>(
                    "default",
                    "passed",
                    requiredSuccessRate: 0,
                    minimumSampleCount: 1,
                    confidenceLevel: 0.95),
                new ExperimentBinarySuccessPolicy<int, int>(
                    "pessimistic",
                    "passed",
                    requiredSuccessRate: 0.9,
                    minimumSampleCount: 1,
                    confidenceLevel: 0.95,
                    unknownSampleTreatment: ExperimentUnknownSampleTreatment.CountAsFailure),
            ],
        };

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 3 },
            _cancellationToken);

        Assert.Equal(EvaluationDecision.Inconclusive, result.PolicyResults[0].Decision);
        Assert.Equal(EvaluationDecision.Failed, result.PolicyResults[1].Decision);
        var defaultEvidence = result.PolicyResults[0].StatisticalEvidence!;
        Assert.Equal(3, defaultEvidence.TotalTrialCount);
        Assert.Equal(2, defaultEvidence.SampleCount);
        Assert.Equal(1, defaultEvidence.SuccessCount);
        Assert.Equal(1, defaultEvidence.FailureCount);
        Assert.Equal(1, defaultEvidence.ExecutionFailureCount);
        Assert.Equal(1, defaultEvidence.ExclusionCount);
        Assert.Equal(
            [
                ExperimentItemStatus.Succeeded,
                ExperimentItemStatus.ExecutionFailed,
                ExperimentItemStatus.TimedOut,
                ExperimentItemStatus.Canceled,
                ExperimentItemStatus.EvaluationFailed,
                ExperimentItemStatus.PrerequisiteFailed,
            ],
            defaultEvidence.StatusCounts.Select(count => count.Status));
        Assert.Equal([2, 1, 0, 0, 0, 0], defaultEvidence.StatusCounts.Select(count => count.Count));
        Assert.Equal(3, result.PolicyResults[1].StatisticalEvidence!.SampleCount);
    }

    [Fact]
    public async Task RunAsync_BinaryPolicy_BelowExplicitMinimumSampleCountIsInconclusive()
    {
        var definition = CreateDefinition(
            trialCount: 3,
            (_, _) => ValueTask.FromResult(new EvaluationResult(
                new BooleanMetric("passed", true))),
            policies:
            [
                new ExperimentBinarySuccessPolicy<int, int>(
                    "minimum-sample",
                    "passed",
                    requiredSuccessRate: 0,
                    minimumSampleCount: 4,
                    confidenceLevel: 0.95),
            ]);

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        var policy = Assert.Single(result.PolicyResults);
        Assert.Equal(EvaluationDecision.Inconclusive, policy.Decision);
        Assert.Equal(3, policy.StatisticalEvidence!.SampleCount);
        Assert.Equal(4, policy.StatisticalEvidence.MinimumSampleCount);
    }

    [Fact]
    public async Task RunAsync_PolicyFailure_IsolatedAsInconclusive()
    {
        var laterPolicyCalls = 0;
        var definition = CreateDefinition(
            trialCount: 1,
            (_, _) => ValueTask.FromResult(new EvaluationResult(
                new BooleanMetric("passed", true))),
            policies:
            [
                new ThrowingExperimentPolicy<int, int>("failure"),
                new CallbackExperimentPolicy<int, int>(
                    "success",
                    () => Interlocked.Increment(ref laterPolicyCalls)),
            ]);

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            _cancellationToken);

        Assert.Equal(1, laterPolicyCalls);
        Assert.Equal(2, result.PolicyResults.Count);
        Assert.Equal(EvaluationDecision.Inconclusive, result.PolicyResults[0].Decision);
        Assert.Equal(ExperimentFailureCode.PolicyFailed, result.PolicyResults[0].Failure!.Code);
        Assert.Equal(ExperimentFailureStage.Policy, result.PolicyResults[0].Failure!.Stage);
        Assert.Equal(EvaluationDecision.Passed, result.PolicyResults[1].Decision);
        Assert.Equal(ExperimentRunDecision.Inconclusive, result.Decision);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationDuringPolicy_PropagatesExactToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var policyStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = CreateDefinition(
            trialCount: 1,
            (_, _) => ValueTask.FromResult(new EvaluationResult(
                new BooleanMetric("passed", true))),
            policies:
            [
                new BlockingExperimentPolicy("blocking", policyStarted),
            ]);
        var runTask = new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 1 },
            cancellation.Token);
        await policyStarted.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private async Task<ExperimentPolicyResult> RunBinaryPolicyAsync(
        string name,
        int trialCount,
        bool metricValue,
        double requiredSuccessRate)
    {
        var definition = CreateDefinition(
            trialCount,
            (_, _) => ValueTask.FromResult(new EvaluationResult(
                new BooleanMetric("passed", metricValue))),
            policies:
            [
                new ExperimentBinarySuccessPolicy<int, int>(
                    name,
                    "passed",
                    requiredSuccessRate,
                    minimumSampleCount: 1,
                    confidenceLevel: 0.95),
            ]);

        var result = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = name, MaxConcurrency = 4 },
            _cancellationToken);

        return Assert.Single(result.PolicyResults);
    }

    private static ExperimentDefinition<int, int> CreateDefinition(
        int trialCount,
        ExperimentItemEvaluator<int, int> evaluator,
        IReadOnlyList<IExperimentRunEvaluator<int, int>>? runEvaluators = null,
        IReadOnlyList<IExperimentRunPolicy<int, int>>? policies = null) =>
        new()
        {
            Name = "policy",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [
                    new ExperimentCase<int>
                    {
                        Id = "case-1",
                        Value = 1,
                        TrialCount = trialCount,
                    },
                ]),
            Task = (context, _) => ValueTask.FromResult(context.TrialIndex),
            ItemEvaluator = evaluator,
            RunEvaluators = runEvaluators ?? [],
            Policies = policies ?? [],
        };
}
