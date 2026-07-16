using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures canonical item, run-evaluation, and decision score projection to Langfuse.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record LangfuseExperimentResultSinkOptions<TCase, TOutput>
{
    /// <summary>Gets the unique generic sink name.</summary>
    public string Name { get; init; } = "langfuse";

    /// <summary>
    /// Gets a value indicating whether sink failure is required for aggregate publication health.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets an optional stable score-id callback for one item metric.
    /// </summary>
    public Func<
        ExperimentItemResult<TCase, TOutput>,
        EvaluationMetric,
        string?>? ItemScoreIdProvider { get; init; }

    /// <summary>
    /// Gets an optional stable score-id callback for one successful run-evaluation metric.
    /// </summary>
    public Func<
        ExperimentRunEvaluationResult,
        EvaluationMetric,
        string?>? RunEvaluationScoreIdProvider { get; init; }

    /// <summary>
    /// Gets optional categorical publication of the already-computed canonical run decision.
    /// </summary>
    public LangfuseExperimentDecisionScoreOptions? DecisionScore { get; init; }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        DecisionScore?.Validate();
    }
}
