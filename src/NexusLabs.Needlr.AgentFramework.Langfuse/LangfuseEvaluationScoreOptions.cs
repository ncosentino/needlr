using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures score identities when projecting an <see cref="EvaluationResult"/> to Langfuse.
/// </summary>
public sealed class LangfuseEvaluationScoreOptions
{
    /// <summary>
    /// Gets or sets a callback that returns the stable Langfuse score id for each metric.
    /// </summary>
    /// <remarks>
    /// Return <see langword="null"/> to publish a metric without an idempotency key. The callback
    /// is invoked once per metric in evaluation-result order.
    /// </remarks>
    public Func<EvaluationMetric, string?>? ScoreIdProvider { get; set; }
}
