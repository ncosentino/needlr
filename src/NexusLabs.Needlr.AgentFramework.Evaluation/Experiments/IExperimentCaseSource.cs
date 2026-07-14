namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Materializes a finite ordered experiment case collection before execution begins.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
public interface IExperimentCaseSource<TCase>
{
    /// <summary>
    /// Loads the complete finite case collection.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The source identity and ordered cases.</returns>
    ValueTask<ExperimentCaseSourceResult<TCase>> LoadAsync(
        CancellationToken cancellationToken = default);
}
