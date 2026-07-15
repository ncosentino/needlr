namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides every ordered item and stable run identity to a run evaluator.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record ExperimentRunEvaluationContext<TCase, TOutput>
{
    internal ExperimentRunEvaluationContext(
        string runId,
        string experimentName,
        ExperimentSourceReference source,
        IReadOnlyList<ExperimentItemResult<TCase, TOutput>> items)
    {
        RunId = runId;
        ExperimentName = experimentName;
        Source = source;
        Items = items;
    }

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the experiment name.</summary>
    public string ExperimentName { get; }

    /// <summary>Gets the materialized source identity.</summary>
    public ExperimentSourceReference Source { get; }

    /// <summary>Gets every item in stable source/trial sequence order.</summary>
    public IReadOnlyList<ExperimentItemResult<TCase, TOutput>> Items { get; }
}
