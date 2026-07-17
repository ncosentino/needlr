namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides one provider lifecycle spanning every attempt and item evaluation for a trial.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public interface IExperimentItemScope<TCase, TOutput> : IAsyncDisposable
{
    /// <summary>
    /// Gets exact-type adapter features exposed to the task and item evaluator.
    /// </summary>
    IReadOnlyDictionary<Type, object> Features { get; }

    /// <summary>
    /// Activates provider context around one task attempt or item-evaluator invocation.
    /// </summary>
    /// <remarks>
    /// The runner invokes this for every activation and disposes the returned handle in reverse
    /// scope order. A <see langword="null"/> handle indicates that no ambient context needs
    /// restoration.
    /// </remarks>
    /// <returns>The context-restoration handle, or <see langword="null"/>.</returns>
    IDisposable? Activate();

    /// <summary>
    /// Receives the terminal quality result and completes provider publication work.
    /// </summary>
    /// <remarks>
    /// This method runs without ambient activation. Implementations must use scope-owned state and
    /// must not mutate the supplied result. The runner calls either this method or
    /// <see cref="AbortAsync"/>, then always calls <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// Once completion starts, caller cancellation is delivered only through
    /// <paramref name="cancellationToken"/>; implementations must not publish a completed item after
    /// observing cancellation.
    /// </remarks>
    /// <param name="result">The terminal execution and evaluation result.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The structured publication operation result.</returns>
    ValueTask<ExperimentItemPublicationOperationResult> CompleteAsync(
        ExperimentItemResult<TCase, TOutput> result,
        CancellationToken cancellationToken);

    /// <summary>
    /// Aborts an incomplete trial after caller cancellation.
    /// </summary>
    /// <remarks>
    /// This method runs without ambient activation and must not publish a completed item record.
    /// </remarks>
    /// <param name="cancellationToken">The bounded cleanup cancellation token.</param>
    /// <returns>A task that completes when abort handling finishes.</returns>
    ValueTask AbortAsync(CancellationToken cancellationToken);
}
