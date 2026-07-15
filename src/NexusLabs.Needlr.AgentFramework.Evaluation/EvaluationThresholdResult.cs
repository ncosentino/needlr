namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Provides the structured result of evaluating configured metric thresholds.
/// </summary>
public sealed record EvaluationThresholdResult
{
    /// <summary>Gets the aggregate quality decision.</summary>
    public required EvaluationDecision Decision { get; init; }

    /// <summary>Gets threshold outcomes in configuration order.</summary>
    public required IReadOnlyList<EvaluationThresholdOutcome> Outcomes { get; init; }
}
