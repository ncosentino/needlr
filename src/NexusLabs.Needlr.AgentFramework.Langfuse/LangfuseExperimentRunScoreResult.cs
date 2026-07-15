namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes the publication outcome for one score against a Langfuse dataset run.
/// </summary>
public sealed record LangfuseExperimentRunScoreResult
{
    /// <summary>
    /// Initializes a dataset-run score result.
    /// </summary>
    /// <param name="scoreId">The caller-supplied Langfuse score id, when present.</param>
    /// <param name="name">The score name.</param>
    /// <param name="status">The publication status.</param>
    /// <param name="datasetRunId">The target dataset-run id, when available.</param>
    /// <param name="failure">The structured publication failure, when present.</param>
    public LangfuseExperimentRunScoreResult(
        string? scoreId,
        string name,
        LangfuseExperimentRunScoreStatus status,
        string? datasetRunId,
        LangfusePublicationFailure? failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "The experiment run score status is not defined.");
        }

        ScoreId = scoreId;
        Name = name;
        Status = status;
        DatasetRunId = datasetRunId;
        Failure = failure;
    }

    /// <summary>
    /// Gets the caller-supplied Langfuse score id, when present.
    /// </summary>
    public string? ScoreId { get; }

    /// <summary>
    /// Gets the score name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the publication status.
    /// </summary>
    public LangfuseExperimentRunScoreStatus Status { get; }

    /// <summary>
    /// Gets the target dataset-run id, when available.
    /// </summary>
    public string? DatasetRunId { get; }

    /// <summary>
    /// Gets the structured publication failure, when present.
    /// </summary>
    public LangfusePublicationFailure? Failure { get; }
}
