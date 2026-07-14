using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Evaluates one terminal successful experiment item exactly once.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
/// <param name="context">The case, trial, output, and attempt history.</param>
/// <param name="cancellationToken">The caller cancellation token.</param>
/// <returns>The MEAI evaluation result.</returns>
public delegate ValueTask<EvaluationResult> ExperimentItemEvaluator<TCase, TOutput>(
    ExperimentItemEvaluationContext<TCase, TOutput> context,
    CancellationToken cancellationToken);
