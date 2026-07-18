using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

internal sealed class BlockingExperimentPolicy(
    string name,
    TaskCompletionSource started) :
    IExperimentRunPolicy<int, int>
{
    public string Name { get; } = name;

    public ExperimentPolicyKind Kind => ExperimentPolicyKind.Deterministic;

    public bool IsRequired => true;

    public async ValueTask<ExperimentPolicyVerdict> EvaluateAsync(
        ExperimentPolicyContext<int, int> context,
        CancellationToken cancellationToken)
    {
        started.SetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Passed);
    }
}
