using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Captures the canonical result delivered to a consumer-defined experiment sink.
/// </summary>
[DoNotAutoRegister]
internal sealed class DualProviderExperimentResultSink<TCase, TOutput> :
    IExperimentResultSink<TCase, TOutput>
{
    public string Name => "consumer-artifacts";

    public bool IsRequired => true;

    public ExperimentRunResult<TCase, TOutput>? PublishedResult { get; private set; }

    public ValueTask<ExperimentSinkPublicationOperationResult> PublishAsync(
        ExperimentRunResult<TCase, TOutput> result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        PublishedResult = result;
        return ValueTask.FromResult(
            ExperimentSinkPublicationOperationResult.Succeeded());
    }
}
