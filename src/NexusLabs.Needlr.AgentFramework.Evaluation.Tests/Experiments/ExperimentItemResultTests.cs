using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentItemResultTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Succeeded_PreservesNullOutputAndDerivesMetricsAndCorrelations()
    {
        var tags = new[] { "tag" };
        var @case = new ExperimentCase<string>
        {
            Id = "case-1",
            Value = "input",
            Tags = tags,
        };
        var publication = ExperimentItemPublicationResult.Succeeded(
            "provider",
            isRequired: false,
            [
                new ExperimentItemCorrelation
                {
                    Namespace = "provider",
                    Name = "trace",
                    Value = "trace-1",
                },
            ]);
        var publications = new[] { publication };
        var evaluation = new EvaluationResult(new NumericMetric("score", 1));

        var result = ExperimentItemResult<string, string?>.Succeeded(
            sequence: 0,
            @case,
            trialIndex: 1,
            [ExperimentAttemptResult.Succeeded(1, StartedAt, TimeSpan.Zero)],
            output: null,
            evaluation,
            publications);
        tags[0] = "changed";
        publications[0] = ExperimentItemPublicationResult.NotAttempted(
            "other",
            isRequired: false,
            correlations: []);
        evaluation.Metrics["later"] = new BooleanMetric("later", true);

        Assert.True(result.HasOutput, "Expected a successful null output to remain present.");
        Assert.Null(result.Output);
        Assert.Equal("tag", Assert.Single(result.Case.Tags));
        Assert.Single(result.Metrics);
        Assert.Equal("trace-1", Assert.Single(result.Correlations).Value);
        Assert.Same(publication, Assert.Single(result.Publications));
    }

    [Fact]
    public void EvaluationFailed_RequiresSuccessfulAttemptAndMatchingFailure()
    {
        var result = ExperimentItemResult<int, int>.EvaluationFailed(
            sequence: 0,
            CreateCase(),
            trialIndex: 1,
            [ExperimentAttemptResult.Succeeded(1, StartedAt, TimeSpan.Zero)],
            output: 7,
            CreateFailure(
                ExperimentFailureCode.EvaluationFailed,
                ExperimentFailureStage.ItemEvaluation),
            publications: []);

        Assert.Equal(ExperimentItemStatus.EvaluationFailed, result.Status);
        Assert.True(result.HasOutput, "Expected evaluation failure to preserve task output.");
        Assert.Equal(7, result.Output);
        Assert.Throws<ArgumentException>(() => ExperimentItemResult<int, int>.EvaluationFailed(
            sequence: 0,
            CreateCase(),
            trialIndex: 1,
            [ExperimentAttemptResult.Unsuccessful(
                1,
                ExperimentAttemptStatus.Failed,
                StartedAt,
                TimeSpan.Zero,
                CreateFailure(
                    ExperimentFailureCode.ExecutionFailed,
                    ExperimentFailureStage.Execution))],
            output: 7,
            CreateFailure(
                ExperimentFailureCode.EvaluationFailed,
                ExperimentFailureStage.ItemEvaluation),
            publications: []));
    }

    [Fact]
    public void Failed_AcceptsRetryPolicyFailureAndRejectsMismatchedStatus()
    {
        var attempt = ExperimentAttemptResult.Unsuccessful(
            1,
            ExperimentAttemptStatus.Failed,
            StartedAt,
            TimeSpan.Zero,
            CreateFailure(
                ExperimentFailureCode.ExecutionFailed,
                ExperimentFailureStage.Execution));
        var retryPolicyFailure = CreateFailure(
            ExperimentFailureCode.RetryPolicyFailed,
            ExperimentFailureStage.Policy);

        var result = ExperimentItemResult<int, int>.Failed(
            sequence: 0,
            CreateCase(),
            trialIndex: 1,
            ExperimentItemStatus.ExecutionFailed,
            [attempt],
            retryPolicyFailure,
            publications: []);

        Assert.Equal(ExperimentFailureCode.RetryPolicyFailed, result.Failure!.Code);
        Assert.Throws<ArgumentException>(() => ExperimentItemResult<int, int>.Failed(
            sequence: 0,
            CreateCase(),
            trialIndex: 1,
            ExperimentItemStatus.TimedOut,
            [attempt],
            CreateFailure(
                ExperimentFailureCode.AttemptTimedOut,
                ExperimentFailureStage.Execution),
            publications: []));
    }

    [Fact]
    public void Factories_RejectNoncontiguousAttemptsAndDuplicatePublicationNames()
    {
        Assert.Throws<ArgumentException>(() => ExperimentItemResult<int, int>.Succeeded(
            sequence: 0,
            CreateCase(),
            trialIndex: 1,
            [ExperimentAttemptResult.Succeeded(2, StartedAt, TimeSpan.Zero)],
            output: 7,
            evaluation: null,
            publications: []));
        var publication = ExperimentItemPublicationResult.NotAttempted(
            "provider",
            isRequired: false,
            correlations: []);
        Assert.Throws<ArgumentException>(() => ExperimentItemResult<int, int>.Succeeded(
            sequence: 0,
            CreateCase(),
            trialIndex: 1,
            [ExperimentAttemptResult.Succeeded(1, StartedAt, TimeSpan.Zero)],
            output: 7,
            evaluation: null,
            [publication, publication]));
    }

    private static ExperimentCase<int> CreateCase() =>
        new()
        {
            Id = "case-1",
            Value = 1,
        };

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
