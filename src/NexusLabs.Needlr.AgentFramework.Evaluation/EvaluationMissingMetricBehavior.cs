namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Configures how required missing or invalid metrics affect a threshold decision.
/// </summary>
public enum EvaluationMissingMetricBehavior
{
    /// <summary>Produce an inconclusive decision.</summary>
    Inconclusive,

    /// <summary>Treat missing or invalid evidence as a failed decision.</summary>
    Fail,
}
