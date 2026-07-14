namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Defines a provider-neutral experiment's finite source and item task.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed class ExperimentDefinition<TCase, TOutput>
{
    /// <summary>Gets the experiment name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the finite case source.</summary>
    public required IExperimentCaseSource<TCase> CaseSource { get; init; }

    /// <summary>Gets the task invoked for each case trial.</summary>
    public required ExperimentTask<TCase, TOutput> Task { get; init; }

    /// <summary>Gets the optional evaluator invoked once after successful task execution.</summary>
    public ExperimentItemEvaluator<TCase, TOutput>? ItemEvaluator { get; init; }

    /// <summary>Gets run evaluators invoked sequentially in registration order.</summary>
    public IReadOnlyList<IExperimentRunEvaluator<TCase, TOutput>> RunEvaluators { get; init; } = [];

    /// <summary>Gets policies invoked sequentially in registration order.</summary>
    public IReadOnlyList<IExperimentRunPolicy<TCase, TOutput>> Policies { get; init; } = [];
}
