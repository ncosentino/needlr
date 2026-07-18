using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

internal sealed class CallbackExperimentPolicy<TCase, TOutput>(
    string name,
    Action callback) :
    IExperimentRunPolicy<TCase, TOutput>
{
    public string Name { get; } = name;

    public ExperimentPolicyKind Kind => ExperimentPolicyKind.Deterministic;

    public bool IsRequired => true;

    public ValueTask<ExperimentPolicyVerdict> EvaluateAsync(
        ExperimentPolicyContext<TCase, TOutput> context,
        CancellationToken cancellationToken)
    {
        callback();
        return ValueTask.FromResult(
            ExperimentPolicyVerdict.WithoutEvidence(EvaluationDecision.Passed));
    }
}
