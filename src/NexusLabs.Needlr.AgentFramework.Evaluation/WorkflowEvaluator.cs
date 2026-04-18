using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Runs a set of <see cref="IEvaluator"/>s against a single captured
/// <see cref="IAgentRunDiagnostics"/> and aggregates the resulting metrics into a
/// <see cref="CompositeEvaluationResult"/>.
/// </summary>
/// <remarks>
/// This is the base composite helper. <see cref="IterativeLoopEvaluator"/> and
/// <see cref="PipelineEvaluator"/> delegate here after selecting the diagnostics to score.
/// Each evaluator is invoked exactly once against <see cref="IAgentRunDiagnostics.InputMessages"/>
/// and the captured <c>AgentResponse</c>; the model is never re-invoked.
/// </remarks>
public static class WorkflowEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="diagnostics"/> against every evaluator in
    /// <paramref name="evaluators"/>.
    /// </summary>
    /// <param name="diagnostics">The captured agent run to evaluate.</param>
    /// <param name="evaluators">The evaluators to run against the captured run.</param>
    /// <param name="chatConfiguration">
    /// The <see cref="ChatConfiguration"/> passed through to each evaluator (for example, the
    /// LLM-as-judge client used by <c>RelevanceEvaluator</c>).
    /// </param>
    /// <param name="cancellationToken">Token for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="CompositeEvaluationResult"/> containing one
    /// <see cref="CompositeEvaluationItem"/> per evaluator, labelled by evaluator name.
    /// </returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<CompositeEvaluationResult> EvaluateAsync(
        IAgentRunDiagnostics diagnostics,
        IEnumerable<IEvaluator> evaluators,
        ChatConfiguration chatConfiguration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(evaluators);
        ArgumentNullException.ThrowIfNull(chatConfiguration);

        var inputs = diagnostics.ToEvaluationInputs();
        var items = new List<CompositeEvaluationItem>();

        foreach (var evaluator in evaluators)
        {
            ArgumentNullException.ThrowIfNull(evaluator);

            var result = await evaluator
                .EvaluateAsync(
                    inputs.Messages,
                    inputs.ModelResponse,
                    chatConfiguration,
                    additionalContext: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            items.Add(new CompositeEvaluationItem(
                Label: evaluator.GetType().Name,
                Inputs: inputs,
                Result: result));
        }

        return new CompositeEvaluationResult(items);
    }
}
