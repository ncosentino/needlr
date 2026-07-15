using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Adapts a named run-evaluation delegate to <see cref="IExperimentRunEvaluator{TCase,TOutput}"/>.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
[DoNotAutoRegister]
public sealed class ExperimentRunEvaluator<TCase, TOutput> :
    IExperimentRunEvaluator<TCase, TOutput>
{
    private readonly ExperimentRunEvaluationHandler<TCase, TOutput> _handler;

    /// <summary>
    /// Initializes a named run evaluator.
    /// </summary>
    /// <param name="name">The stable evaluator name.</param>
    /// <param name="handler">The evaluation handler.</param>
    public ExperimentRunEvaluator(
        string name,
        ExperimentRunEvaluationHandler<TCase, TOutput> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        Name = name;
        _handler = handler;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        ExperimentRunEvaluationContext<TCase, TOutput> context,
        CancellationToken cancellationToken) =>
        _handler(context, cancellationToken);
}
