using System.Reflection;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentPublicationOperationResultTests
{
    [Fact]
    public void OperationContracts_ExcludeRegisteredIdentityAndArbitraryStateConstruction()
    {
        AssertOperationContract(
            typeof(ExperimentItemPublicationOperationResult),
            ["Correlations", "Failure", "Status"]);
        AssertOperationContract(
            typeof(ExperimentSinkPublicationOperationResult),
            ["Failure", "Status"]);

        Assert.Equal(
            typeof(ValueTask<ExperimentItemPublicationOperationResult>),
            typeof(IExperimentItemScope<int, int>)
                .GetMethod(nameof(IExperimentItemScope<int, int>.CompleteAsync))!
                .ReturnType);
        Assert.Equal(
            typeof(ValueTask<ExperimentSinkPublicationOperationResult>),
            typeof(IExperimentResultSink<int, int>)
                .GetMethod(nameof(IExperimentResultSink<int, int>.PublishAsync))!
                .ReturnType);
    }

    [Fact]
    public void ItemFactories_EnforceStateAndSnapshotCorrelationLists()
    {
        var succeededSource = CreateCorrelationArray("succeeded");
        var notAttemptedSource = CreateCorrelationArray("not-attempted");
        var failedSource = CreateCorrelationArray("failed");
        var failure = CreateFailure(ExperimentFailureCode.ItemScopeFailed);

        var succeeded =
            ExperimentItemPublicationOperationResult.Succeeded(succeededSource);
        var notAttempted =
            ExperimentItemPublicationOperationResult.NotAttempted(notAttemptedSource);
        var failed =
            ExperimentItemPublicationOperationResult.Failed(failedSource, failure);
        var succeededClone = succeeded with { };

        succeededSource[0] = CreateCorrelation("replacement");
        notAttemptedSource[0] = CreateCorrelation("replacement");
        failedSource[0] = CreateCorrelation("replacement");

        Assert.NotSame(succeeded, succeededClone);
        Assert.Equal(succeeded, succeededClone);
        AssertOperation(
            succeeded,
            ExperimentPublicationOperationStatus.Succeeded,
            "succeeded",
            failure: null);
        AssertOperation(
            notAttempted,
            ExperimentPublicationOperationStatus.NotAttempted,
            "not-attempted",
            failure: null);
        AssertOperation(
            failed,
            ExperimentPublicationOperationStatus.Failed,
            "failed",
            failure);
    }

    [Fact]
    public void SinkFactories_EnforceStateAndFailurePresence()
    {
        var failure = CreateFailure(ExperimentFailureCode.ResultSinkFailed);

        var succeeded = ExperimentSinkPublicationOperationResult.Succeeded();
        var notAttempted = ExperimentSinkPublicationOperationResult.NotAttempted();
        var failed = ExperimentSinkPublicationOperationResult.Failed(failure);
        var failedClone = failed with { };

        Assert.NotSame(failed, failedClone);
        Assert.Equal(failed, failedClone);
        Assert.Equal(ExperimentPublicationOperationStatus.Succeeded, succeeded.Status);
        Assert.Null(succeeded.Failure);
        Assert.Equal(
            ExperimentPublicationOperationStatus.NotAttempted,
            notAttempted.Status);
        Assert.Null(notAttempted.Failure);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, failed.Status);
        Assert.Equal(failure, failed.Failure);
    }

    [Fact]
    public void Factories_RejectNullRequiredState()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentItemPublicationOperationResult.Succeeded(null!));
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentItemPublicationOperationResult.NotAttempted(null!));
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentItemPublicationOperationResult.Succeeded([null!]));
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentItemPublicationOperationResult.NotAttempted([null!]));
        var failure = CreateFailure(ExperimentFailureCode.ItemScopeFailed);
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentItemPublicationOperationResult.Failed(
                null!,
                failure));
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentItemPublicationOperationResult.Failed(
                [null!],
                failure));
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentItemPublicationOperationResult.Failed(
                [],
                null!));
        Assert.Throws<ArgumentNullException>(() =>
            ExperimentSinkPublicationOperationResult.Failed(null!));
    }

    private static void AssertOperationContract(
        Type type,
        IReadOnlyList<string> expectedProperties)
    {
        Assert.True(type.IsClass, "Expected the publication operation contract to be a class.");
        Assert.True(type.IsSealed, "Expected the publication operation contract to be sealed.");
        Assert.Empty(type.GetConstructors());
        var properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedProperties, properties.Select(property => property.Name));
        Assert.All(
            properties,
            property => Assert.False(
                property.SetMethod?.IsPublic ?? false,
                $"Expected property '{property.Name}' to remain read-only."));
        Assert.DoesNotContain(properties, property => property.Name == "Name");
        Assert.DoesNotContain(properties, property => property.Name == "IsRequired");
    }

    private static void AssertOperation(
        ExperimentItemPublicationOperationResult result,
        ExperimentPublicationOperationStatus expectedStatus,
        string expectedCorrelationValue,
        ExperimentFailure? failure)
    {
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(
            expectedCorrelationValue,
            Assert.Single(result.Correlations).Value);
        Assert.Equal(failure, result.Failure);
    }

    private static ExperimentItemCorrelation[] CreateCorrelationArray(string value) =>
        [CreateCorrelation(value)];

    private static ExperimentItemCorrelation CreateCorrelation(string value) =>
        new()
        {
            Namespace = "provider",
            Name = "item",
            Value = value,
        };

    private static ExperimentFailure CreateFailure(ExperimentFailureCode code) =>
        new(
            code,
            ExperimentFailureStage.Publication,
            typeof(InvalidOperationException).FullName!,
            "publication failed",
            isRetryable: false);
}
