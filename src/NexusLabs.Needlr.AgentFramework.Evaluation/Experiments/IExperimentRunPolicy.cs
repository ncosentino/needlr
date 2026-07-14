namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Produces a structured decision from complete experiment measurements.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public interface IExperimentRunPolicy<TCase, TOutput>
{
    /// <summary>Gets the stable policy name.</summary>
    string Name { get; }

    /// <summary>Gets the evidence model used by the policy.</summary>
    ExperimentPolicyKind Kind { get; }

    /// <summary>Gets a value indicating whether this policy contributes to the run decision.</summary>
    bool IsRequired { get; }

    /// <summary>
    /// Evaluates complete experiment measurements.
    /// </summary>
    /// <param name="context">The complete policy context.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The structured policy evaluation.</returns>
    ValueTask<ExperimentPolicyVerdict> EvaluateAsync(
        ExperimentPolicyContext<TCase, TOutput> context,
        CancellationToken cancellationToken);
}
