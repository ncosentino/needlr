namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides an immutable Needlr-owned projection of one MEAI evaluation metric.
/// </summary>
public sealed class ExperimentMetricSnapshot
{
    /// <summary>Gets the metric name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the normalized metric value kind.</summary>
    public required ExperimentMetricKind Kind { get; init; }

    /// <summary>Gets the numeric value when <see cref="Kind"/> is numeric.</summary>
    public double? NumericValue { get; init; }

    /// <summary>Gets the explicit non-finite numeric value classification, when present.</summary>
    public ExperimentMetricNonFiniteValue? NonFiniteNumericValue { get; init; }

    /// <summary>Gets the boolean value when <see cref="Kind"/> is boolean.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the string value when <see cref="Kind"/> is string.</summary>
    public string? StringValue { get; init; }

    /// <summary>Gets the optional metric reason.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the optional normalized interpretation.</summary>
    public ExperimentMetricInterpretationSnapshot? Interpretation { get; init; }

    /// <summary>
    /// Gets the number of MEAI context objects omitted from the schema-v1 snapshot.
    /// </summary>
    public required int ContextCount { get; init; }

    /// <summary>Gets the copied diagnostic collection.</summary>
    public required IReadOnlyList<ExperimentMetricDiagnosticSnapshot> Diagnostics { get; init; }

    /// <summary>Gets copied string metadata in ordinal key order.</summary>
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
