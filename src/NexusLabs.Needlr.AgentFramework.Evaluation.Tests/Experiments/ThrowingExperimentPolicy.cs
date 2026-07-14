using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

internal sealed class ThrowingExperimentPolicy<TCase, TOutput>(string name) :
    IExperimentRunPolicy<TCase, TOutput>
{
    public string Name { get; } = name;

    public ExperimentPolicyKind Kind => ExperimentPolicyKind.Deterministic;

    public bool IsRequired => true;

    public ValueTask<ExperimentPolicyVerdict> EvaluateAsync(
        ExperimentPolicyContext<TCase, TOutput> context,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("policy failed");
}
