using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Measures a complete ordered experiment run.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public interface IExperimentRunEvaluator<TCase, TOutput>
{
    /// <summary>Gets the stable evaluator name.</summary>
    string Name { get; }

    /// <summary>
    /// Evaluates every ordered item in the run.
    /// </summary>
    /// <param name="context">The complete ordered run context.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The run-level MEAI evaluation result.</returns>
    ValueTask<EvaluationResult> EvaluateAsync(
        ExperimentRunEvaluationContext<TCase, TOutput> context,
        CancellationToken cancellationToken);
}
