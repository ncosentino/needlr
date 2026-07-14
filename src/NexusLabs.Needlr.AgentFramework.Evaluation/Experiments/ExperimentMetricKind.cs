namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the normalized value kind of an experiment metric snapshot.
/// </summary>
public enum ExperimentMetricKind
{
    /// <summary>The metric contains a nullable numeric value.</summary>
    Numeric,

    /// <summary>The metric contains a nullable boolean value.</summary>
    Boolean,

    /// <summary>The metric contains a nullable string value.</summary>
    String,

    /// <summary>The metric is the MEAI base metric with no typed value.</summary>
    None,

    /// <summary>The metric is a custom type that is not recognized by this schema version.</summary>
    Unknown,
}
