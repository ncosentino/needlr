using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Publishes dataset-run scores while exposing each structured outcome before strict failure
/// propagation.
/// </summary>
internal interface ILangfuseExperimentScorePublisher
{
    Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result,
        LangfuseEvaluationScoreOptions? options,
        Action<LangfuseExperimentRunScoreResult> observer,
        CancellationToken cancellationToken);

    Task<LangfuseExperimentRunScoreResult> RecordCategoricalScoreAsync(
        string name,
        string value,
        LangfuseScoreOptions? options,
        Action<LangfuseExperimentRunScoreResult> observer,
        CancellationToken cancellationToken);
}
