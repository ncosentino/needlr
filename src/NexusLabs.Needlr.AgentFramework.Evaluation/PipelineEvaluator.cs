using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Evaluates every stage of an <see cref="IPipelineRunResult"/> by delegating to
/// <see cref="WorkflowEvaluator"/> once per stage that captured diagnostics.
/// </summary>
/// <remarks>
/// Each stage in the pipeline is scored independently with the supplied evaluator set.
/// Stages whose <see cref="IAgentStageResult.Diagnostics"/> is <see langword="null"/>
/// (diagnostics not enabled) are silently skipped. Item labels in the returned result combine
/// the stage's <see cref="IAgentStageResult.AgentName"/> with the evaluator's type name so
/// callers can trace every metric back to both the stage and the evaluator that produced it.
/// </remarks>
public static class PipelineEvaluator
{
    /// <summary>
    /// Evaluates every stage of <paramref name="pipelineResult"/> against every evaluator in
    /// <paramref name="evaluators"/>.
    /// </summary>
    /// <param name="pipelineResult">The pipeline run result to evaluate.</param>
    /// <param name="evaluators">The evaluators to run against each stage's diagnostics.</param>
    /// <param name="chatConfiguration">
    /// The <see cref="ChatConfiguration"/> passed through to each evaluator.
    /// </param>
    /// <param name="cancellationToken">Token for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="CompositeEvaluationResult"/> containing one item per (stage, evaluator) pair
    /// for every stage that captured diagnostics. Stages without diagnostics are skipped.
    /// </returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<CompositeEvaluationResult> EvaluateAsync(
        IPipelineRunResult pipelineResult,
        IEnumerable<IEvaluator> evaluators,
        ChatConfiguration chatConfiguration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipelineResult);
        ArgumentNullException.ThrowIfNull(evaluators);
        ArgumentNullException.ThrowIfNull(chatConfiguration);

        var evaluatorList = evaluators as IReadOnlyList<IEvaluator> ?? evaluators.ToList();
        foreach (var e in evaluatorList)
        {
            ArgumentNullException.ThrowIfNull(e);
        }

        var items = new List<CompositeEvaluationItem>();

        foreach (var stage in pipelineResult.Stages)
        {
            if (stage.Diagnostics is null)
            {
                continue;
            }

            var inputs = stage.Diagnostics.ToEvaluationInputs();

            foreach (var evaluator in evaluatorList)
            {
                var result = await evaluator
                    .EvaluateAsync(
                        inputs.Messages,
                        inputs.ModelResponse,
                        chatConfiguration,
                        additionalContext: null,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                items.Add(new CompositeEvaluationItem(
                    Label: $"{stage.AgentName}:{evaluator.GetType().Name}",
                    Inputs: inputs,
                    Result: result));
            }
        }

        return new CompositeEvaluationResult(items);
    }
}
