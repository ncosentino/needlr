using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures optional categorical publication of the canonical experiment decision.
/// </summary>
public sealed record LangfuseExperimentDecisionScoreOptions
{
    /// <summary>Gets the decision score name.</summary>
    public string Name { get; init; } = "experiment_decision";

    /// <summary>
    /// Gets an optional callback that returns the stable score id for the canonical decision.
    /// </summary>
    public Func<ExperimentRunDecision, string?>? ScoreIdProvider { get; init; }

    /// <summary>Gets an optional comment attached to the decision score.</summary>
    public string? Comment { get; init; }

    internal void Validate() =>
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
}
