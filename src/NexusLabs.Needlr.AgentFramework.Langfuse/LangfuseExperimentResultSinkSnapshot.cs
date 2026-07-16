namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Captures the latest detailed score projection performed by a Langfuse experiment result sink.
/// </summary>
public sealed record LangfuseExperimentResultSinkSnapshot
{
    /// <summary>Gets aggregate direct score-publication status.</summary>
    public required LangfuseExperimentApiPublicationStatus ScorePublicationStatus { get; init; }

    /// <summary>Gets ordered item trace-score results.</summary>
    public IReadOnlyList<LangfuseExperimentItemScoreResult> ItemScores { get; init; } = [];

    /// <summary>Gets ordered dataset-run score results grouped by run evaluator.</summary>
    public IReadOnlyList<LangfuseExperimentRunEvaluationScoreResult> RunEvaluationScores { get; init; } = [];

    /// <summary>Gets the optional categorical canonical-decision score result.</summary>
    public LangfuseExperimentRunScoreResult? DecisionScore { get; init; }

    /// <summary>
    /// Gets the hosted run's link and dataset-run score snapshot, or <see langword="null"/> in local
    /// mode.
    /// </summary>
    public LangfuseExperimentRunPublicationSnapshot? ExperimentRunPublication { get; init; }
}
