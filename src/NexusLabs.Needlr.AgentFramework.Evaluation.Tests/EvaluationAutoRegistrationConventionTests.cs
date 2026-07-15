using System.Reflection;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class EvaluationAutoRegistrationConventionTests
{
    private const BindingFlags RecordPropertyFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void DataContracts_AreRecords()
    {
        Type[] dataContracts =
        [
            typeof(CapturedChatResponsePayload),
            typeof(CapturedChatResponsePayload.CapturedMessage),
            typeof(CapturedChatResponsePayload.CapturedContent),
            typeof(EvaluationThresholdOutcome),
            typeof(EvaluationThresholdResult),
            typeof(ExperimentAttemptResult),
            typeof(ExperimentBinaryStatisticalEvidence),
            typeof(ExperimentCase<>),
            typeof(ExperimentCaseSourceResult<>),
            typeof(ExperimentDefinition<,>),
            typeof(ExperimentDeterministicPolicyEvidence),
            typeof(ExperimentFailure),
            typeof(ExperimentItemCorrelation),
            typeof(ExperimentItemEvaluationContext<,>),
            typeof(ExperimentItemPublicationResult),
            typeof(ExperimentItemResult<,>),
            typeof(ExperimentItemScopeContext<>),
            typeof(ExperimentItemStatusCount),
            typeof(ExperimentMetricDiagnosticSnapshot),
            typeof(ExperimentMetricInterpretationSnapshot),
            typeof(ExperimentMetricSnapshot),
            typeof(ExperimentPolicyContext<,>),
            typeof(ExperimentPolicyResult),
            typeof(ExperimentPolicyVerdict),
            typeof(ExperimentRetryContext),
            typeof(ExperimentRetryDecision),
            typeof(ExperimentRunEvaluationContext<,>),
            typeof(ExperimentRunEvaluationResult),
            typeof(ExperimentRunOptions),
            typeof(ExperimentRunResult<,>),
            typeof(ExperimentSourceReference),
            typeof(ExperimentTaskContext<>),
        ];

        Assert.All(dataContracts, AssertRecord);
    }

    [Fact]
    public void ConcreteNonServiceClasses_OptOutOfAutomaticRegistration()
    {
        HashSet<Type> intendedServices =
        [
            typeof(EfficiencyEvaluator),
            typeof(EvaluationCaptureChatClient),
            typeof(FileEvaluationCaptureStore),
            typeof(IterationCoherenceEvaluator),
            typeof(PipelineCostEvaluator),
            typeof(PipelineStageEvaluator),
            typeof(TaskCompletionEvaluator),
            typeof(TerminationAppropriatenessEvaluator),
            typeof(ToolCallTrajectoryEvaluator),
            typeof(ExperimentRunner),
        ];
        var unsafeTypes = typeof(EvaluationQualityGate).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && !type.IsNested
                && type.Namespace?.StartsWith(
                    "NexusLabs.Needlr.AgentFramework.Evaluation",
                    StringComparison.Ordinal) == true
                && !typeof(Delegate).IsAssignableFrom(type)
                && !typeof(Exception).IsAssignableFrom(type)
                && !IsRecord(type)
                && !intendedServices.Contains(type)
                && !type.IsDefined(typeof(DoNotAutoRegisterAttribute), inherit: true))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unsafeTypes);
    }

    private static void AssertRecord(Type type) =>
        Assert.True(
            IsRecord(type),
            $"Expected data contract '{type.FullName}' to be declared as a record.");

    private static bool IsRecord(Type type) =>
        type.GetProperty("EqualityContract", RecordPropertyFlags) is not null;
}
