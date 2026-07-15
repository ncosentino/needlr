using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

/// <summary>
/// Publishes experiment results through a caller-supplied test callback.
/// </summary>
internal sealed class CallbackExperimentResultSink<TCase, TOutput>(
    string name,
    bool isRequired,
    Func<
        ExperimentRunResult<TCase, TOutput>,
        CancellationToken,
        ValueTask<ExperimentSinkResult>> publishAsync) :
    IExperimentResultSink<TCase, TOutput>
{
    public string Name => name;

    public bool IsRequired => isRequired;

    public ValueTask<ExperimentSinkResult> PublishAsync(
        ExperimentRunResult<TCase, TOutput> result,
        CancellationToken cancellationToken) =>
        publishAsync(result, cancellationToken);
}
