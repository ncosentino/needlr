namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Creates one provider-neutral lifecycle scope for each statistical trial.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public interface IExperimentItemScopeProvider<TCase, TOutput>
{
    /// <summary>Gets the unique provider name used in item publication results.</summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether publication failure is required for aggregate publication
    /// health.
    /// </summary>
    bool IsRequired { get; }

    /// <summary>Gets the behavior for entry or activation failure before a task attempt.</summary>
    ExperimentItemScopeFailureMode FailureMode { get; }

    /// <summary>
    /// Enters one scope for a statistical trial.
    /// </summary>
    /// <remarks>
    /// The runner invokes this once per trial under the caller token and shared concurrency lease,
    /// but outside the per-attempt timeout. The returned scope remains alive across retry delays.
    /// </remarks>
    /// <param name="context">The stable trial identity and case data.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The entered item scope.</returns>
    ValueTask<IExperimentItemScope<TCase, TOutput>> EnterAsync(
        ExperimentItemScopeContext<TCase> context,
        CancellationToken cancellationToken);
}
