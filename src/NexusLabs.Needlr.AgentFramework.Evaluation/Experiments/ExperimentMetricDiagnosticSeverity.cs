namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies the stable severity of a normalized metric diagnostic.
/// </summary>
public enum ExperimentMetricDiagnosticSeverity
{
    /// <summary>The diagnostic is informational.</summary>
    Informational,

    /// <summary>The diagnostic is a warning.</summary>
    Warning,

    /// <summary>The diagnostic is an error.</summary>
    Error,

    /// <summary>The source severity is not recognized.</summary>
    Unknown,
}
