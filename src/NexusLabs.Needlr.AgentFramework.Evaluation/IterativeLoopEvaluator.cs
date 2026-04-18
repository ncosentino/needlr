using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Evaluates an <see cref="IterativeLoopResult"/> by delegating to
/// <see cref="WorkflowEvaluator"/> against the loop's aggregate diagnostics.
/// </summary>
/// <remarks>
/// An iterative loop exposes a single aggregate <see cref="IAgentRunDiagnostics"/> covering the
/// entire loop. This helper scores that aggregate with the supplied evaluators. When the result
/// has no diagnostics (for example, diagnostics were not enabled on the loop), an empty
/// <see cref="CompositeEvaluationResult"/> is returned rather than throwing.
/// </remarks>
public static class IterativeLoopEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="loopResult"/> against every evaluator in
    /// <paramref name="evaluators"/>.
    /// </summary>
    /// <param name="loopResult">The iterative loop result to evaluate.</param>
    /// <param name="evaluators">The evaluators to run against the loop's diagnostics.</param>
    /// <param name="chatConfiguration">
    /// The <see cref="ChatConfiguration"/> passed through to each evaluator.
    /// </param>
    /// <param name="cancellationToken">Token for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="CompositeEvaluationResult"/> containing the evaluator outcomes, or an empty
    /// result when <see cref="IterativeLoopResult.Diagnostics"/> is <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static async Task<CompositeEvaluationResult> EvaluateAsync(
        IterativeLoopResult loopResult,
        IEnumerable<IEvaluator> evaluators,
        ChatConfiguration chatConfiguration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loopResult);
        ArgumentNullException.ThrowIfNull(evaluators);
        ArgumentNullException.ThrowIfNull(chatConfiguration);

        if (loopResult.Diagnostics is null)
        {
            return new CompositeEvaluationResult(Array.Empty<CompositeEvaluationItem>());
        }

        return await WorkflowEvaluator
            .EvaluateAsync(
                loopResult.Diagnostics,
                evaluators,
                chatConfiguration,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
