namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Represents an in-progress Langfuse experiment (dataset run). Each call to
/// <see cref="BeginItemAsync"/> starts a trace for one dataset item and links it to this run, so
/// the scores recorded on that trace roll up into the run's experiment-comparison view.
/// </summary>
/// <remarks>
/// The referenced dataset and its items must already exist (see
/// <see cref="ILangfuseDatasetClient"/>). The run itself is created implicitly by Langfuse on the
/// first linked item. Run names are caller-supplied (for example a git SHA or CI run id) so runs
/// are comparable and reproducible.
/// </remarks>
public interface ILangfuseExperimentRun
{
    /// <summary>Gets the dataset this run is scored against.</summary>
    string DatasetName { get; }

    /// <summary>Gets the run name (for example a git SHA or CI run id).</summary>
    string RunName { get; }

    /// <summary>
    /// Begins a scenario for one dataset item and links its trace to this run as a dataset-run-item.
    /// Run your agent under the returned scenario and record scores on it as usual; they aggregate
    /// into the experiment view for this run.
    /// </summary>
    /// <param name="datasetItemId">The id of the dataset item being evaluated.</param>
    /// <param name="scenarioName">
    /// An optional trace name. Defaults to a name derived from the dataset and item id.
    /// </param>
    /// <param name="tags">Optional trace tags.</param>
    /// <param name="metadata">Optional filterable trace metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The scenario for this item. Dispose it when the item run completes.</returns>
    Task<ILangfuseScenario> BeginItemAsync(
        string datasetItemId,
        string? scenarioName = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);
}
