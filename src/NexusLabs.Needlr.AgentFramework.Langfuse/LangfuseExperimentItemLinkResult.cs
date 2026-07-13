namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes the direct REST publication outcome for one dataset-run-item link.
/// </summary>
public sealed class LangfuseExperimentItemLinkResult
{
    /// <summary>
    /// Initializes an item-link result.
    /// </summary>
    /// <param name="status">The link status.</param>
    /// <param name="datasetRunItemId">The created dataset-run-item id, when available.</param>
    /// <param name="datasetRunId">The dataset-run id returned for this item, when available.</param>
    /// <param name="failure">The structured failure, when present.</param>
    public LangfuseExperimentItemLinkResult(
        LangfuseExperimentItemLinkStatus status,
        string? datasetRunItemId,
        string? datasetRunId,
        LangfusePublicationFailure? failure)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "The experiment item link status is not defined.");
        }

        Status = status;
        DatasetRunItemId = datasetRunItemId;
        DatasetRunId = datasetRunId;
        Failure = failure;
    }

    /// <summary>
    /// Gets the link status.
    /// </summary>
    public LangfuseExperimentItemLinkStatus Status { get; }

    /// <summary>
    /// Gets the dataset-run-item id returned by Langfuse, when available.
    /// </summary>
    public string? DatasetRunItemId { get; }

    /// <summary>
    /// Gets the dataset-run id returned for this item, when available.
    /// </summary>
    public string? DatasetRunId { get; }

    /// <summary>
    /// Gets the structured publication failure, when present.
    /// </summary>
    public LangfusePublicationFailure? Failure { get; }
}
