namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes one canonical item metric projected to a Langfuse trace score.
/// </summary>
public sealed record LangfuseExperimentItemScoreResult
{
    /// <summary>Gets the canonical case id.</summary>
    public required string CaseId { get; init; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public required int TrialIndex { get; init; }

    /// <summary>Gets the target trace id, when available.</summary>
    public string? TraceId { get; init; }

    /// <summary>Gets the requested or accepted score id, when available.</summary>
    public string? ScoreId { get; init; }

    /// <summary>Gets the normalized score name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the direct score publication status.</summary>
    public required LangfuseExperimentScoreStatus Status { get; init; }

    /// <summary>Gets the structured provider failure, when present.</summary>
    public LangfusePublicationFailure? Failure { get; init; }
}
