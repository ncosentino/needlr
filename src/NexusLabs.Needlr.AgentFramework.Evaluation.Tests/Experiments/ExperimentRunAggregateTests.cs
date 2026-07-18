using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunAggregateTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(EvaluationDecision.Passed, ExperimentRunDecision.Passed)]
    [InlineData(EvaluationDecision.Failed, ExperimentRunDecision.Failed)]
    [InlineData(EvaluationDecision.Inconclusive, ExperimentRunDecision.Inconclusive)]
    public void RunResult_DerivesDecisionFromRequiredPolicies(
        EvaluationDecision policyDecision,
        ExperimentRunDecision expectedDecision)
    {
        var result = CreateRunResult(
            [
                ExperimentPolicyResult.FromVerdict(
                    "required",
                    ExperimentPolicyKind.Deterministic,
                    isRequired: true,
                    ExperimentPolicyVerdict.WithoutEvidence(policyDecision)),
                ExperimentPolicyResult.FromVerdict(
                    "optional",
                    ExperimentPolicyKind.Deterministic,
                    isRequired: false,
                    ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Failed)),
            ]);

        Assert.Equal(expectedDecision, result.Decision);
        Assert.Equal(ExperimentRunResult<int, int>.CurrentSchemaVersion, result.SchemaVersion);
    }

    [Fact]
    public void RunResult_NoRequiredPolicies_IsNotEvaluated()
    {
        var result = CreateRunResult([]);

        Assert.Equal(ExperimentRunDecision.NotEvaluated, result.Decision);
    }

    [Fact]
    public void RunResult_RejectsInvalidOrderingAndDuplicatePolicyNames()
    {
        var item = CreateItem(sequence: 0, publications: []);
        Assert.Throws<ArgumentException>(() => new ExperimentRunResult<int, int>(
            "run",
            "experiment",
            new ExperimentSourceReference { Name = "source" },
            StartedAt,
            TimeSpan.Zero,
            maxConcurrency: 1,
            workerCount: 1,
            [item with { }],
            runEvaluations: [],
            policyResults:
            [
                ExperimentPolicyResult.FromVerdict(
                    "duplicate",
                    ExperimentPolicyKind.Deterministic,
                    isRequired: true,
                    ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Passed)),
                ExperimentPolicyResult.FromVerdict(
                    "duplicate",
                    ExperimentPolicyKind.Deterministic,
                    isRequired: false,
                    ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Passed)),
            ]));

        var second = CreateItem(sequence: 0, publications: []);
        Assert.Throws<ArgumentException>(() => new ExperimentRunResult<int, int>(
            "run",
            "experiment",
            new ExperimentSourceReference { Name = "source" },
            StartedAt,
            TimeSpan.Zero,
            maxConcurrency: 2,
            workerCount: 2,
            [item, second],
            runEvaluations: [],
            policyResults: []));
    }

    [Fact]
    public void Outcome_DerivesPublicationStatusFromItemsAndSinks()
    {
        var optionalItemFailure = ExperimentItemPublicationResult.Failed(
            "item",
            isRequired: false,
            correlations: [],
            CreateFailure(ExperimentFailureCode.ItemScopeFailed));
        var partial = new ExperimentRunOutcome<int, int>(
            CreateRunResult([], [optionalItemFailure]),
            [ExperimentSinkResult.Succeeded("sink", isRequired: true)]);
        var failed = new ExperimentRunOutcome<int, int>(
            CreateRunResult([]),
            [
                ExperimentSinkResult.Failed(
                    "sink",
                    isRequired: true,
                    CreateFailure(ExperimentFailureCode.ResultSinkFailed)),
            ]);
        var notRequested = new ExperimentRunOutcome<int, int>(
            CreateRunResult([]),
            [ExperimentSinkResult.NotAttempted("sink", isRequired: true)]);

        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, partial.PublicationStatus);
        Assert.Equal(ExperimentPublicationStatus.Failed, failed.PublicationStatus);
        Assert.Equal(ExperimentPublicationStatus.NotRequested, notRequested.PublicationStatus);
        Assert.Equal(
            ExperimentRunOutcome<int, int>.CurrentSchemaVersion,
            partial.SchemaVersion);
    }

    [Fact]
    public void Outcome_RejectsDuplicateSinkNames()
    {
        Assert.Throws<ArgumentException>(() => new ExperimentRunOutcome<int, int>(
            CreateRunResult([]),
            [
                ExperimentSinkResult.Succeeded("sink", isRequired: false),
                ExperimentSinkResult.NotAttempted("sink", isRequired: true),
            ]));
    }

    [Fact]
    public void Constructors_SnapshotOwnedCollections()
    {
        var items = new[] { CreateItem(sequence: 0, publications: []) };
        var policies = new[]
        {
            ExperimentPolicyResult.FromVerdict(
                "required",
                ExperimentPolicyKind.Deterministic,
                isRequired: true,
                ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Passed)),
        };
        var result = new ExperimentRunResult<int, int>(
            "run",
            "experiment",
            new ExperimentSourceReference { Name = "source" },
            StartedAt,
            TimeSpan.Zero,
            maxConcurrency: 1,
            workerCount: 1,
            items,
            runEvaluations: [],
            policies);
        var sinks = new[] { ExperimentSinkResult.Succeeded("sink", isRequired: false) };
        var outcome = new ExperimentRunOutcome<int, int>(result, sinks);
        items[0] = CreateItem(sequence: 0, publications: []);
        policies[0] = ExperimentPolicyResult.FromVerdict(
            "changed",
            ExperimentPolicyKind.Deterministic,
            isRequired: true,
            ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Failed));
        sinks[0] = ExperimentSinkResult.NotAttempted("changed", isRequired: true);

        Assert.NotSame(items, result.Items);
        Assert.Equal("required", Assert.Single(result.PolicyResults).Name);
        Assert.Equal("sink", Assert.Single(outcome.SinkResults).Name);
    }

    private static ExperimentRunResult<int, int> CreateRunResult(
        IReadOnlyList<ExperimentPolicyResult> policies) =>
        CreateRunResult(policies, publications: []);

    private static ExperimentRunResult<int, int> CreateRunResult(
        IReadOnlyList<ExperimentPolicyResult> policies,
        IReadOnlyList<ExperimentItemPublicationResult> publications) =>
        new(
            "run",
            "experiment",
            new ExperimentSourceReference { Name = "source" },
            StartedAt,
            TimeSpan.FromSeconds(1),
            maxConcurrency: 1,
            workerCount: 1,
            [CreateItem(sequence: 0, publications)],
            runEvaluations: [],
            policies);

    private static ExperimentItemResult<int, int> CreateItem(
        int sequence,
        IReadOnlyList<ExperimentItemPublicationResult> publications) =>
        ExperimentItemResult<int, int>.Succeeded(
            sequence,
            new ExperimentCase<int> { Id = "case-1", Value = 1 },
            trialIndex: 1,
            [ExperimentAttemptResult.Succeeded(1, StartedAt, TimeSpan.Zero)],
            output: 1,
            evaluation: null,
            publications);

    private static ExperimentFailure CreateFailure(ExperimentFailureCode code) =>
        new(
            code,
            ExperimentFailureStage.Publication,
            typeof(InvalidOperationException).FullName!,
            "failed",
            isRetryable: false);
}
