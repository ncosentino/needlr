namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Identifies the observed outcome of one evaluation threshold.
/// </summary>
public enum EvaluationThresholdStatus
{
    /// <summary>The metric satisfies the threshold.</summary>
    Passed,

    /// <summary>The metric violates the threshold.</summary>
    Failed,

    /// <summary>No metric with the required name was available.</summary>
    Missing,

    /// <summary>The metric was present but did not contain a valid value of the required kind.</summary>
    Invalid,
}
