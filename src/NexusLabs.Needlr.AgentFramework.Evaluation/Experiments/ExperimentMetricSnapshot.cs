using System.Collections.ObjectModel;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides an immutable Needlr-owned projection of one MEAI evaluation metric.
/// </summary>
/// <remarks>
/// Instances are constructed only by <see cref="ExperimentMetricSnapshotFactory"/> from a MEAI
/// <see cref="Microsoft.Extensions.AI.Evaluation.EvaluationResult"/>. External evaluators return
/// MEAI evaluation results and never construct snapshots directly.
/// </remarks>
public sealed record ExperimentMetricSnapshot
{
    /// <summary>
    /// Initializes a validated metric snapshot and takes an independent copy of both collections.
    /// </summary>
    /// <param name="name">The metric name.</param>
    /// <param name="kind">The normalized metric value kind.</param>
    /// <param name="numericValue">The finite numeric value, when the kind is numeric.</param>
    /// <param name="nonFiniteNumericValue">
    /// The non-finite numeric value classification, permitted only when the kind is numeric.
    /// </param>
    /// <param name="booleanValue">The boolean value, when the kind is boolean.</param>
    /// <param name="stringValue">The string value, when the kind is string.</param>
    /// <param name="reason">The optional metric reason.</param>
    /// <param name="interpretation">The optional normalized interpretation.</param>
    /// <param name="contextCount">The number of MEAI context objects omitted from the snapshot.</param>
    /// <param name="diagnostics">The diagnostics to copy into the snapshot.</param>
    /// <param name="metadata">The string metadata to copy into the snapshot.</param>
    internal ExperimentMetricSnapshot(
        string name,
        ExperimentMetricKind kind,
        double? numericValue,
        ExperimentMetricNonFiniteValue? nonFiniteNumericValue,
        bool? booleanValue,
        string? stringValue,
        string? reason,
        ExperimentMetricInterpretationSnapshot? interpretation,
        int contextCount,
        IReadOnlyList<ExperimentMetricDiagnosticSnapshot> diagnostics,
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The metric kind is not defined.");
        }

        if (nonFiniteNumericValue is { } nonFinite && !Enum.IsDefined(nonFinite))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nonFiniteNumericValue),
                nonFinite,
                "The non-finite numeric value is not defined.");
        }

        ValidateValueRepresentation(
            kind,
            numericValue,
            nonFiniteNumericValue,
            booleanValue,
            stringValue);
        if (contextCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextCount),
                contextCount,
                "The metric context count must be non-negative.");
        }

        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(metadata);
        var diagnosticsSnapshot = new ExperimentMetricDiagnosticSnapshot[diagnostics.Count];
        for (var index = 0; index < diagnostics.Count; index++)
        {
            var diagnostic = diagnostics[index];
            ArgumentNullException.ThrowIfNull(diagnostic);
            diagnosticsSnapshot[index] = diagnostic;
        }

        var metadataSnapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in metadata)
        {
            metadataSnapshot[entry.Key] = entry.Value;
        }

        Name = name;
        Kind = kind;
        NumericValue = numericValue;
        NonFiniteNumericValue = nonFiniteNumericValue;
        BooleanValue = booleanValue;
        StringValue = stringValue;
        Reason = reason;
        Interpretation = interpretation;
        ContextCount = contextCount;
        Diagnostics = Array.AsReadOnly(diagnosticsSnapshot);
        Metadata = new ReadOnlyDictionary<string, string>(metadataSnapshot);
    }

    /// <summary>Gets the metric name.</summary>
    public string Name { get; }

    /// <summary>Gets the normalized metric value kind.</summary>
    public ExperimentMetricKind Kind { get; }

    /// <summary>Gets the numeric value when <see cref="Kind"/> is numeric.</summary>
    public double? NumericValue { get; }

    /// <summary>Gets the explicit non-finite numeric value classification, when present.</summary>
    public ExperimentMetricNonFiniteValue? NonFiniteNumericValue { get; }

    /// <summary>Gets the boolean value when <see cref="Kind"/> is boolean.</summary>
    public bool? BooleanValue { get; }

    /// <summary>Gets the string value when <see cref="Kind"/> is string.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the optional metric reason.</summary>
    public string? Reason { get; }

    /// <summary>Gets the optional normalized interpretation.</summary>
    public ExperimentMetricInterpretationSnapshot? Interpretation { get; }

    /// <summary>
    /// Gets the number of MEAI context objects omitted from the canonical snapshot.
    /// </summary>
    public int ContextCount { get; }

    /// <summary>Gets the copied diagnostic collection.</summary>
    public IReadOnlyList<ExperimentMetricDiagnosticSnapshot> Diagnostics { get; }

    /// <summary>Gets copied string metadata in ordinal key order.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static void ValidateValueRepresentation(
        ExperimentMetricKind kind,
        double? numericValue,
        ExperimentMetricNonFiniteValue? nonFiniteNumericValue,
        bool? booleanValue,
        string? stringValue)
    {
        switch (kind)
        {
            case ExperimentMetricKind.Numeric:
                if (booleanValue is not null || stringValue is not null)
                {
                    throw new ArgumentException(
                        "A numeric metric snapshot cannot carry a boolean or string value.");
                }

                if (numericValue is not null && nonFiniteNumericValue is not null)
                {
                    throw new ArgumentException(
                        "A numeric metric snapshot cannot set both a finite and a non-finite value.");
                }

                break;
            case ExperimentMetricKind.Boolean:
                if (numericValue is not null
                    || nonFiniteNumericValue is not null
                    || stringValue is not null)
                {
                    throw new ArgumentException(
                        "A boolean metric snapshot can only carry a boolean value.");
                }

                break;
            case ExperimentMetricKind.String:
                if (numericValue is not null
                    || nonFiniteNumericValue is not null
                    || booleanValue is not null)
                {
                    throw new ArgumentException(
                        "A string metric snapshot can only carry a string value.");
                }

                break;
            default:
                if (numericValue is not null
                    || nonFiniteNumericValue is not null
                    || booleanValue is not null
                    || stringValue is not null)
                {
                    throw new ArgumentException(
                        $"A '{kind}' metric snapshot cannot carry a typed value.");
                }

                break;
        }
    }
}
