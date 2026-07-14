namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Identifies the comparison performed by an evaluation threshold.
/// </summary>
public enum EvaluationThresholdKind
{
    /// <summary>A numeric metric must not exceed a maximum.</summary>
    NumericMaximum,

    /// <summary>A numeric metric must meet a minimum.</summary>
    NumericMinimum,

    /// <summary>A boolean metric must equal an expected value.</summary>
    Boolean,
}
