using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentPolicyValueTests
{
    [Fact]
    public void DeterministicEvidence_FactoriesEnforceExclusiveStateAndSnapshotOutcomes()
    {
        var outcomes = new List<EvaluationThresholdOutcome>
        {
            new()
            {
                MetricName = "quality",
                Kind = EvaluationThresholdKind.NumericMinimum,
                Status = EvaluationThresholdStatus.Passed,
                IsRequired = true,
                NumericThreshold = 0.8,
                NumericValue = 0.9,
                Message = "passed",
            },
        };
        var thresholdResult = new EvaluationThresholdResult
        {
            Decision = EvaluationDecision.Passed,
            Outcomes = outcomes,
        };

        var available = ExperimentDeterministicPolicyEvidence.Available(
            "aggregate",
            thresholdResult);
        var unavailable = ExperimentDeterministicPolicyEvidence.Unavailable(
            "aggregate",
            "metrics unavailable");
        outcomes.Clear();

        Assert.Single(available.Thresholds!.Outcomes);
        Assert.Null(available.UnavailableReason);
        Assert.Null(unavailable.Thresholds);
        Assert.Equal("metrics unavailable", unavailable.UnavailableReason);
        Assert.Throws<ArgumentException>(() =>
            ExperimentDeterministicPolicyEvidence.Unavailable("aggregate", " "));
    }

    [Fact]
    public void BinaryEvidence_DerivesStableCountsAndWilsonBounds()
    {
        var evidence = CreateBinaryEvidence();

        Assert.Equal(5, evidence.TotalTrialCount);
        Assert.Equal(4, evidence.SampleCount);
        Assert.Equal(2, evidence.SuccessCount);
        Assert.Equal(2, evidence.FailureCount);
        Assert.Equal(1, evidence.ExecutionFailureCount);
        Assert.Equal(1, evidence.ExclusionCount);
        Assert.Equal(0.5, evidence.Estimate);
        Assert.NotNull(evidence.OneSidedLowerBound);
        Assert.NotNull(evidence.OneSidedUpperBound);
        Assert.Equal(
            Enum.GetValues<ExperimentItemStatus>(),
            evidence.StatusCounts.Select(status => status.Status));
    }

    [Fact]
    public void BinaryEvidence_RejectsIncompleteAndInconsistentAccounting()
    {
        var incomplete = CreateStatusCounts().Skip(1).ToArray();
        Assert.Throws<ArgumentException>(() => ExperimentBinaryStatisticalEvidence.Create(
            "passed",
            attemptCount: 5,
            successCount: 2,
            failureCount: 2,
            exclusionCount: 1,
            incomplete,
            confidenceLevel: 0.95,
            requiredSuccessRate: 0.8,
            minimumSampleCount: 1,
            ExperimentConfidenceIntervalMethod.WilsonScore,
            ExperimentUnknownSampleTreatment.Inconclusive));

        Assert.Throws<ArgumentException>(() => ExperimentBinaryStatisticalEvidence.Create(
            "passed",
            attemptCount: 5,
            successCount: 2,
            failureCount: 1,
            exclusionCount: 1,
            CreateStatusCounts(),
            confidenceLevel: 0.95,
            requiredSuccessRate: 0.8,
            minimumSampleCount: 1,
            ExperimentConfidenceIntervalMethod.WilsonScore,
            ExperimentUnknownSampleTreatment.Inconclusive));

        Assert.Throws<ArgumentException>(() => ExperimentBinaryStatisticalEvidence.Create(
            "passed",
            attemptCount: 5,
            successCount: 4,
            failureCount: 1,
            exclusionCount: 2,
            CreateStatusCounts(),
            confidenceLevel: 0.95,
            requiredSuccessRate: 0.8,
            minimumSampleCount: 1,
            ExperimentConfidenceIntervalMethod.WilsonScore,
            ExperimentUnknownSampleTreatment.CountAsFailure));

        Assert.Throws<ArgumentException>(() => ExperimentBinaryStatisticalEvidence.Create(
            "passed",
            attemptCount: 0,
            successCount: 2,
            failureCount: 2,
            exclusionCount: 1,
            CreateStatusCounts(),
            confidenceLevel: 0.95,
            requiredSuccessRate: 0.8,
            minimumSampleCount: 1,
            ExperimentConfidenceIntervalMethod.WilsonScore,
            ExperimentUnknownSampleTreatment.Inconclusive));
    }

    [Fact]
    public void PolicyVerdict_FactoriesEnforceEvidenceShapeAndDeriveStatisticalDecision()
    {
        var thresholdResult = new EvaluationThresholdResult
        {
            Decision = EvaluationDecision.Passed,
            Outcomes = [],
        };
        var deterministicEvidence = ExperimentDeterministicPolicyEvidence.Available(
            "aggregate",
            thresholdResult);

        var deterministic = ExperimentPolicyVerdict.FromDeterministicEvidence(
            EvaluationDecision.Passed,
            deterministicEvidence);
        var statistical = ExperimentPolicyVerdict.FromStatisticalEvidence(
            CreateBinaryEvidence());

        Assert.Equal(EvaluationDecision.Passed, deterministic.Decision);
        Assert.Same(deterministicEvidence, deterministic.DeterministicEvidence);
        Assert.Equal(EvaluationDecision.Inconclusive, statistical.Decision);
        Assert.NotNull(statistical.StatisticalEvidence);
        Assert.Throws<ArgumentException>(() =>
            ExperimentPolicyVerdict.FromDeterministicEvidence(
                EvaluationDecision.Failed,
                deterministicEvidence));
    }

    [Fact]
    public void PolicyResult_FactoriesApplyIdentityAndValidateFailureShape()
    {
        var verdict = ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Passed);
        var result = ExperimentPolicyResult.FromVerdict(
            "quality",
            ExperimentPolicyKind.Deterministic,
            isRequired: true,
            verdict);
        var failure = CreateFailure(
            ExperimentFailureCode.PolicyFailed,
            ExperimentFailureStage.Policy);
        var failed = ExperimentPolicyResult.ExecutionFailed(
            "quality",
            ExperimentPolicyKind.Deterministic,
            isRequired: true,
            failure);

        Assert.Equal(EvaluationDecision.Passed, result.Decision);
        Assert.True(result.IsRequired, "Expected the registered required flag to be preserved.");
        Assert.Equal(EvaluationDecision.Inconclusive, failed.Decision);
        Assert.NotSame(failure, failed.Failure);
        Assert.Throws<ArgumentException>(() => ExperimentPolicyResult.ExecutionFailed(
            "quality",
            ExperimentPolicyKind.Deterministic,
            isRequired: true,
            CreateFailure(
                ExperimentFailureCode.RunEvaluationFailed,
                ExperimentFailureStage.RunEvaluation)));
    }

    [Fact]
    public void RunEvaluationResult_FactoriesNormalizeSuccessAndValidateFailure()
    {
        var evaluation = new EvaluationResult(new NumericMetric("score", 0.9));
        var succeeded = ExperimentRunEvaluationResult.Succeeded("aggregate", evaluation);
        evaluation.Metrics["later"] = new BooleanMetric("later", true);
        var failure = CreateFailure(
            ExperimentFailureCode.RunEvaluationFailed,
            ExperimentFailureStage.RunEvaluation);
        var failed = ExperimentRunEvaluationResult.Failed("aggregate", failure);

        Assert.Equal(ExperimentRunEvaluationStatus.Succeeded, succeeded.Status);
        Assert.Single(succeeded.Metrics);
        Assert.Same(evaluation, succeeded.Evaluation);
        Assert.Equal(ExperimentRunEvaluationStatus.Failed, failed.Status);
        Assert.Empty(failed.Metrics);
        Assert.NotSame(failure, failed.Failure);
        Assert.Throws<ArgumentException>(() => ExperimentRunEvaluationResult.Failed(
            "aggregate",
            CreateFailure(
                ExperimentFailureCode.PolicyFailed,
                ExperimentFailureStage.Policy)));
    }

    private static ExperimentBinaryStatisticalEvidence CreateBinaryEvidence() =>
        ExperimentBinaryStatisticalEvidence.Create(
            "passed",
            attemptCount: 5,
            successCount: 2,
            failureCount: 2,
            exclusionCount: 1,
            CreateStatusCounts(),
            confidenceLevel: 0.95,
            requiredSuccessRate: 0.8,
            minimumSampleCount: 1,
            ExperimentConfidenceIntervalMethod.WilsonScore,
            ExperimentUnknownSampleTreatment.Inconclusive);

    private static IReadOnlyList<ExperimentItemStatusCount> CreateStatusCounts() =>
    [
        new(ExperimentItemStatus.PrerequisiteFailed, 0),
        new(ExperimentItemStatus.Succeeded, 3),
        new(ExperimentItemStatus.Canceled, 0),
        new(ExperimentItemStatus.ExecutionFailed, 1),
        new(ExperimentItemStatus.EvaluationFailed, 1),
        new(ExperimentItemStatus.TimedOut, 0),
    ];

    private static ExperimentFailure CreateFailure(
        ExperimentFailureCode code,
        ExperimentFailureStage stage) =>
        new(
            code,
            stage,
            typeof(InvalidOperationException).FullName!,
            "failed",
            isRetryable: false);
}
