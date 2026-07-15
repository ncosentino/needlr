namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides immutable run measurements to an experiment policy.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record ExperimentPolicyContext<TCase, TOutput>
{
    internal ExperimentPolicyContext(
        string runId,
        string experimentName,
        ExperimentSourceReference source,
        IReadOnlyList<ExperimentItemResult<TCase, TOutput>> items,
        IReadOnlyList<ExperimentRunEvaluationResult> runEvaluations)
    {
        RunId = runId;
        ExperimentName = experimentName;
        Source = source;
        Items = items;
        RunEvaluations = runEvaluations;
    }

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the experiment name.</summary>
    public string ExperimentName { get; }

    /// <summary>Gets the materialized source identity.</summary>
    public ExperimentSourceReference Source { get; }

    /// <summary>Gets every item in stable source/trial sequence order.</summary>
    public IReadOnlyList<ExperimentItemResult<TCase, TOutput>> Items { get; }

    /// <summary>Gets run-evaluation results in registration order.</summary>
    public IReadOnlyList<ExperimentRunEvaluationResult> RunEvaluations { get; }
}
