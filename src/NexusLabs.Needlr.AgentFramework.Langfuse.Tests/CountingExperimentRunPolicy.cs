using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Counts policy evaluation while returning one configured decision.
/// </summary>
internal sealed class CountingExperimentRunPolicy<TCase, TOutput>(
    EvaluationDecision decision) :
    IExperimentRunPolicy<TCase, TOutput>
{
    private int _invocationCount;

    public string Name => "counting-policy";

    public ExperimentPolicyKind Kind => ExperimentPolicyKind.Deterministic;

    public bool IsRequired => true;

    public int InvocationCount => Volatile.Read(ref _invocationCount);

    public ValueTask<ExperimentPolicyVerdict> EvaluateAsync(
        ExperimentPolicyContext<TCase, TOutput> context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _invocationCount);
        return ValueTask.FromResult(
            ExperimentPolicyVerdict.WithoutEvidence(decision));
    }
}
