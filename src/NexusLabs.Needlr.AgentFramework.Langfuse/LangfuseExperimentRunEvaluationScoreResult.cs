namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes one successful canonical run-evaluation metric projected to a dataset-run score.
/// </summary>
public sealed record LangfuseExperimentRunEvaluationScoreResult
{
    /// <summary>Gets the canonical run-evaluator name.</summary>
    public required string EvaluatorName { get; init; }

    /// <summary>Gets the requested or accepted score id, when available.</summary>
    public string? ScoreId { get; init; }

    /// <summary>Gets the normalized score name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the direct score publication status.</summary>
    public required LangfuseExperimentScoreStatus Status { get; init; }

    /// <summary>Gets the target dataset-run id, when available.</summary>
    public string? DatasetRunId { get; init; }

    /// <summary>Gets the structured provider failure, when present.</summary>
    public LangfusePublicationFailure? Failure { get; init; }
}
