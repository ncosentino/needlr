using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Measures a complete ordered experiment run.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
/// <param name="context">The complete ordered run context.</param>
/// <param name="cancellationToken">The caller cancellation token.</param>
/// <returns>The run-level MEAI evaluation result.</returns>
public delegate ValueTask<EvaluationResult> ExperimentRunEvaluationHandler<TCase, TOutput>(
    ExperimentRunEvaluationContext<TCase, TOutput> context,
    CancellationToken cancellationToken);
