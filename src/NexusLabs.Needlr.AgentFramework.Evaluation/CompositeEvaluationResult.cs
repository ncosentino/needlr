namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Aggregated output of a composite evaluator (<see cref="WorkflowEvaluator"/>,
/// <see cref="IterativeLoopEvaluator"/>, or <see cref="PipelineEvaluator"/>), listing every
/// evaluator-per-subject pair that was scored along with its produced metrics.
/// </summary>
/// <param name="Items">
/// The per-evaluator (and per-stage, where applicable) outcomes in the order they were produced.
/// May be empty when no diagnostics were available to evaluate.
/// </param>
public sealed record CompositeEvaluationResult(
    IReadOnlyList<CompositeEvaluationItem> Items);
