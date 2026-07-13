using System.Text.Json;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Represents an in-progress Langfuse experiment (dataset run). Each call to
/// <see cref="RunItemAsync{T}(string, Func{ILangfuseScenario, CancellationToken, Task{T}}, LangfuseExperimentItemOptions?, CancellationToken)"/>
/// executes one dataset item inside an active scenario trace and links it to this run, so scores
/// recorded on that trace roll up into the experiment-comparison view.
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

    /// <summary>Gets the requested run description.</summary>
    string? Description { get; }

    /// <summary>Gets the frozen structured metadata submitted with item links.</summary>
    JsonElement? Metadata { get; }

    /// <summary>
    /// Gets the authoritative dataset-run id after successful links agree on one identity.
    /// </summary>
    string? DatasetRunId { get; }

    /// <summary>Gets the aggregate dataset-run identity status.</summary>
    LangfuseDatasetRunIdentityStatus IdentityStatus { get; }

    /// <summary>
    /// Executes a callback while one dataset item's scenario is active and links the scenario trace
    /// to this run as a dataset-run-item.
    /// </summary>
    /// <remarks>
    /// The scenario is valid only for the callback lifetime and must not be retained. Callback
    /// exceptions and caller-requested cancellation propagate unchanged after the scenario is
    /// disposed and the previous ambient activity is restored.
    /// </remarks>
    /// <typeparam name="T">The callback result type.</typeparam>
    /// <param name="datasetItemId">The id of the dataset item being evaluated.</param>
    /// <param name="callback">
    /// The item work to execute. The supplied scenario is active as <see cref="System.Diagnostics.Activity.Current"/>
    /// for the callback lifetime and is disposed before this method completes.
    /// </param>
    /// <param name="options">Optional scenario and dataset-link behavior.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The callback value, trace id, and structured dataset-link result.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="datasetItemId"/> is <see langword="null"/> or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="LangfuseExperimentItemOptions.LinkFailureMode"/> is not defined.
    /// </exception>
    /// <exception cref="LangfuseException">
    /// Langfuse could not link the item and strict link-failure mode was selected.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before or during linking or callback execution.
    /// </exception>
    Task<LangfuseExperimentItemResult<T>> RunItemAsync<T>(
        string datasetItemId,
        Func<ILangfuseScenario, CancellationToken, Task<T>> callback,
        LangfuseExperimentItemOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records a numeric score against the resolved dataset run.</summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The numeric value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The direct score-publication outcome.</returns>
    /// <exception cref="LangfuseException">
    /// The dataset-run identity is unavailable or score publication failed while strict score mode
    /// is configured.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        double value,
        string? comment = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records a boolean score against the resolved dataset run.</summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The boolean value.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The direct score-publication outcome.</returns>
    /// <exception cref="LangfuseException">
    /// The dataset-run identity is unavailable or score publication failed while strict score mode
    /// is configured.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        bool value,
        string? comment = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records a categorical score against the resolved dataset run.</summary>
    /// <param name="name">The score name.</param>
    /// <param name="value">The category label.</param>
    /// <param name="comment">An optional explanation surfaced in Langfuse.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The direct score-publication outcome.</returns>
    /// <exception cref="LangfuseException">
    /// The dataset-run identity is unavailable or score publication failed while strict score mode
    /// is configured.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<LangfuseExperimentRunScoreResult> RecordScoreAsync(
        string name,
        string value,
        string? comment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects every metric in <paramref name="result"/> to a score against the resolved dataset
    /// run.
    /// </summary>
    /// <param name="result">The evaluation result to project.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One structured score result per evaluation metric.</returns>
    /// <exception cref="LangfuseException">
    /// The dataset-run identity is unavailable or score publication failed while strict score mode
    /// is configured.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<LangfuseExperimentRunScoreResult>> RecordEvaluationAsync(
        EvaluationResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an immutable snapshot of direct item-link and run-score API outcomes observed by this
    /// run instance.
    /// </summary>
    /// <returns>The current publication snapshot.</returns>
    LangfuseExperimentRunPublicationSnapshot GetPublicationSnapshot();
}
