namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a stable provider-owned projection of MEAI metric ratings.
/// </summary>
public enum ExperimentMetricRating
{
    /// <summary>The rating is unknown.</summary>
    Unknown,

    /// <summary>The metric cannot be interpreted conclusively.</summary>
    Inconclusive,

    /// <summary>The metric is unacceptable.</summary>
    Unacceptable,

    /// <summary>The metric is poor.</summary>
    Poor,

    /// <summary>The metric is average.</summary>
    Average,

    /// <summary>The metric is good.</summary>
    Good,

    /// <summary>The metric is exceptional.</summary>
    Exceptional,
}
