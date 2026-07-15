namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Runs finite provider-neutral experiments with bounded task concurrency.
/// </summary>
public interface IExperimentRunner
{
    /// <summary>
    /// Materializes, validates, expands, and executes one experiment.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="definition">The experiment definition.</param>
    /// <param name="options">The run options.</param>
    /// <param name="cancellationToken">A caller cancellation token.</param>
    /// <returns>The canonical quality result plus independent publication outcomes.</returns>
    Task<ExperimentRunOutcome<TCase, TOutput>> RunAsync<TCase, TOutput>(
        ExperimentDefinition<TCase, TOutput> definition,
        ExperimentRunOptions options,
        CancellationToken cancellationToken = default);
}
