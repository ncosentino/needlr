namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the confidence interval used by a statistical policy.
/// </summary>
public enum ExperimentConfidenceIntervalMethod
{
    /// <summary>A one-sided Wilson score interval without continuity correction.</summary>
    WilsonScore,
}
