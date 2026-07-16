namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Provides an immutable snapshot of direct REST publication observed by one experiment run.
/// </summary>
/// <remarks>
/// The snapshot covers item-link and dataset-run score API calls made through this run instance.
/// It does not verify that OpenTelemetry traces were durably ingested by Langfuse.
/// </remarks>
public sealed record LangfuseExperimentRunPublicationSnapshot
{
    /// <summary>
    /// Initializes a publication snapshot.
    /// </summary>
    /// <param name="identityStatus">The aggregate dataset-run identity status.</param>
    /// <param name="datasetRunId">The authoritative dataset-run id, when available.</param>
    /// <param name="operationsInFlight">The number of public run operations still in progress.</param>
    /// <param name="itemLinks">The item-link outcome counts.</param>
    /// <param name="runScores">The run-score outcome counts.</param>
    /// <param name="apiPublicationStatus">The aggregate direct API publication status.</param>
    public LangfuseExperimentRunPublicationSnapshot(
        LangfuseDatasetRunIdentityStatus identityStatus,
        string? datasetRunId,
        int operationsInFlight,
        LangfuseExperimentItemLinkCounts itemLinks,
        LangfuseExperimentScoreCounts runScores,
        LangfuseExperimentApiPublicationStatus apiPublicationStatus)
    {
        if (!Enum.IsDefined(identityStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(identityStatus), identityStatus, "The dataset run identity status is not defined.");
        }

        if (!Enum.IsDefined(apiPublicationStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(apiPublicationStatus), apiPublicationStatus, "The API publication status is not defined.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(operationsInFlight);
        ArgumentNullException.ThrowIfNull(itemLinks);
        ArgumentNullException.ThrowIfNull(runScores);

        IdentityStatus = identityStatus;
        DatasetRunId = datasetRunId;
        OperationsInFlight = operationsInFlight;
        ItemLinks = itemLinks;
        RunScores = runScores;
        ApiPublicationStatus = apiPublicationStatus;
    }

    /// <summary>Gets the aggregate dataset-run identity status.</summary>
    public LangfuseDatasetRunIdentityStatus IdentityStatus { get; }

    /// <summary>Gets the authoritative dataset-run id, when resolved and consistent.</summary>
    public string? DatasetRunId { get; }

    /// <summary>Gets the number of public run operations currently awaiting completion.</summary>
    public int OperationsInFlight { get; }

    /// <summary>Gets item-link outcome counts.</summary>
    public LangfuseExperimentItemLinkCounts ItemLinks { get; }

    /// <summary>Gets dataset-run score outcome counts.</summary>
    public LangfuseExperimentScoreCounts RunScores { get; }

    /// <summary>Gets the aggregate direct API publication status.</summary>
    public LangfuseExperimentApiPublicationStatus ApiPublicationStatus { get; }
}
