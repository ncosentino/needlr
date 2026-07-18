using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentCanonicalPublicationResultTests
{
    [Fact]
    public void ItemFactories_SnapshotIdentityCorrelationsAndFailure()
    {
        var correlations = new[]
        {
            new ExperimentItemCorrelation
            {
                Namespace = "provider",
                Name = "trace",
                Value = "trace-1",
            },
        };
        var succeeded = ExperimentItemPublicationResult.Succeeded(
            "provider",
            isRequired: false,
            correlations);
        var failure = CreateFailure(ExperimentFailureCode.ItemScopeFailed);
        var failed = ExperimentItemPublicationResult.Failed(
            "provider",
            isRequired: true,
            correlations,
            failure);
        correlations[0] = new ExperimentItemCorrelation
        {
            Namespace = "changed",
            Name = "trace",
            Value = "trace-2",
        };

        Assert.Equal("trace-1", Assert.Single(succeeded.Correlations).Value);
        Assert.True(failed.IsRequired, "Expected the registered required flag to be preserved.");
        Assert.NotSame(failure, failed.Failure);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, failed.Status);
    }

    [Fact]
    public void ItemFactories_RejectDuplicateCorrelationsAndInvalidFailure()
    {
        var duplicate =
            new ExperimentItemCorrelation
            {
                Namespace = "provider",
                Name = "trace",
                Value = "trace-1",
            };
        Assert.Throws<ArgumentException>(() => ExperimentItemPublicationResult.Succeeded(
            "provider",
            isRequired: false,
            [duplicate, duplicate with { Value = "trace-2" }]));
        Assert.Throws<InvalidOperationException>(() => ExperimentItemPublicationResult.Failed(
            "provider",
            isRequired: false,
            [],
            CreateFailure(ExperimentFailureCode.ResultSinkFailed)));
    }

    [Fact]
    public void SinkFactories_EnforceRegisteredIdentityAndFailureShape()
    {
        var succeeded = ExperimentSinkResult.Succeeded("sink", isRequired: false);
        var failure = CreateFailure(ExperimentFailureCode.ResultSinkFailed);
        var failed = ExperimentSinkResult.Failed("sink", isRequired: true, failure);

        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, succeeded.Status);
        Assert.False(succeeded.IsRequired, "Expected the optional sink flag to be preserved.");
        Assert.NotSame(failure, failed.Failure);
        Assert.Throws<InvalidOperationException>(() => ExperimentSinkResult.Failed(
            "sink",
            isRequired: false,
            CreateFailure(ExperimentFailureCode.ItemScopeFailed)));
    }

    private static ExperimentFailure CreateFailure(ExperimentFailureCode code) =>
        new(
            code,
            ExperimentFailureStage.Publication,
            typeof(InvalidOperationException).FullName!,
            "publication failed",
            isRetryable: false);
}
