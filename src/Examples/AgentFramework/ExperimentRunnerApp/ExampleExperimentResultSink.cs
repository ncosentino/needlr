using NexusLabs.Needlr;
using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace ExperimentRunnerApp;

/// <summary>
/// Demonstrates credential-free final publication without changing experiment quality.
/// </summary>
[DoNotAutoRegister]
internal sealed class ExampleExperimentResultSink :
    IExperimentResultSink<ExperimentCaseDefinition, ExperimentOutput>
{
    public string Name => "credential-free-local";

    public bool IsRequired => true;

    public ValueTask<ExperimentSinkResult> PublishAsync(
        ExperimentRunResult<ExperimentCaseDefinition, ExperimentOutput> result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(
            $"  Sink '{Name}' observed {result.Items.Count} finalized trials.");
        return ValueTask.FromResult(new ExperimentSinkResult
        {
            Name = Name,
            IsRequired = IsRequired,
            Status = ExperimentPublicationOperationStatus.Succeeded,
        });
    }
}
