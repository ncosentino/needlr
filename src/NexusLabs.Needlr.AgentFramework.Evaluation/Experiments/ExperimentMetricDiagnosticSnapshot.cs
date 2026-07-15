namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides an immutable normalized metric diagnostic.
/// </summary>
public sealed record ExperimentMetricDiagnosticSnapshot
{
    /// <summary>Gets the normalized diagnostic severity.</summary>
    public required ExperimentMetricDiagnosticSeverity Severity { get; init; }

    /// <summary>Gets the diagnostic message.</summary>
    public required string Message { get; init; }
}
