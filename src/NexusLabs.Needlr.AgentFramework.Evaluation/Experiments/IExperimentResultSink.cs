namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Publishes a completed canonical experiment result without changing its quality decision.
/// </summary>
/// <remarks>
/// Needlr-owned collections are read-only snapshots. Caller-owned case/output values and MEAI
/// evaluation objects cannot be deeply frozen and must be treated as read-only by implementations.
/// Generic retry is not applied; a sink owns retry only for provider operations it can prove
/// idempotent.
/// </remarks>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public interface IExperimentResultSink<TCase, TOutput>
{
    /// <summary>Gets the unique sink name.</summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether failure contributes to aggregate required-publication
    /// failure.
    /// </summary>
    bool IsRequired { get; }

    /// <summary>
    /// Publishes one completed canonical result.
    /// </summary>
    /// <remarks>
    /// Throw to let the runner synthesize a structured failed result, or return a conforming failed
    /// result when the adapter already owns a provider-specific failure classification.
    /// </remarks>
    /// <param name="result">The read-only canonical quality result.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The structured publication operation result.</returns>
    ValueTask<ExperimentSinkPublicationOperationResult> PublishAsync(
        ExperimentRunResult<TCase, TOutput> result,
        CancellationToken cancellationToken);
}
