using System.Collections.ObjectModel;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Freezes mutable MEAI metrics into deterministic Needlr-owned snapshots.
/// </summary>
internal static class ExperimentMetricSnapshotFactory
{
    public static IReadOnlyList<ExperimentMetricSnapshot> Create(
        EvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        ArgumentNullException.ThrowIfNull(evaluation.Metrics);
        var snapshots = evaluation.Metrics
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry =>
            {
                ArgumentNullException.ThrowIfNull(entry.Value);
                if (!string.Equals(
                    entry.Key,
                    entry.Value.Name,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Evaluation metric dictionary key '{entry.Key}' does not match metric name '{entry.Value.Name}'.");
                }

                return Create(entry.Value);
            })
            .ToArray();
        return Array.AsReadOnly(snapshots);
    }

    private static ExperimentMetricSnapshot Create(EvaluationMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        var diagnostics = (metric.Diagnostics ?? [])
            .Select(diagnostic => new ExperimentMetricDiagnosticSnapshot
            {
                Severity = diagnostic.Severity switch
                {
                    EvaluationDiagnosticSeverity.Informational =>
                        ExperimentMetricDiagnosticSeverity.Informational,
                    EvaluationDiagnosticSeverity.Warning =>
                        ExperimentMetricDiagnosticSeverity.Warning,
                    EvaluationDiagnosticSeverity.Error =>
                        ExperimentMetricDiagnosticSeverity.Error,
                    _ => ExperimentMetricDiagnosticSeverity.Unknown,
                },
                Message = diagnostic.Message,
            })
            .ToArray();
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (metric.Metadata is not null)
        {
            foreach (var entry in metric.Metadata)
            {
                metadata[entry.Key] = entry.Value;
            }
        }

        var kind = metric switch
        {
            NumericMetric => ExperimentMetricKind.Numeric,
            BooleanMetric => ExperimentMetricKind.Boolean,
            StringMetric => ExperimentMetricKind.String,
            _ when metric.GetType() == typeof(EvaluationMetric) =>
                ExperimentMetricKind.None,
            _ => ExperimentMetricKind.Unknown,
        };
        var numericValue = (metric as NumericMetric)?.Value;
        ExperimentMetricNonFiniteValue? nonFiniteNumericValue = numericValue switch
        {
            double value when double.IsNaN(value) =>
                ExperimentMetricNonFiniteValue.NaN,
            double value when double.IsPositiveInfinity(value) =>
                ExperimentMetricNonFiniteValue.PositiveInfinity,
            double value when double.IsNegativeInfinity(value) =>
                ExperimentMetricNonFiniteValue.NegativeInfinity,
            _ => null,
        };
        return new ExperimentMetricSnapshot
        {
            Name = metric.Name,
            Kind = kind,
            NumericValue = nonFiniteNumericValue is null ? numericValue : null,
            NonFiniteNumericValue = nonFiniteNumericValue,
            BooleanValue = (metric as BooleanMetric)?.Value,
            StringValue = (metric as StringMetric)?.Value,
            Reason = metric.Reason,
            Interpretation = metric.Interpretation is null
                ? null
                : new ExperimentMetricInterpretationSnapshot
                {
                    Rating = metric.Interpretation.Rating switch
                    {
                        EvaluationRating.Unknown => ExperimentMetricRating.Unknown,
                        EvaluationRating.Inconclusive => ExperimentMetricRating.Inconclusive,
                        EvaluationRating.Unacceptable => ExperimentMetricRating.Unacceptable,
                        EvaluationRating.Poor => ExperimentMetricRating.Poor,
                        EvaluationRating.Average => ExperimentMetricRating.Average,
                        EvaluationRating.Good => ExperimentMetricRating.Good,
                        EvaluationRating.Exceptional => ExperimentMetricRating.Exceptional,
                        _ => ExperimentMetricRating.Unknown,
                    },
                    Failed = metric.Interpretation.Failed,
                    Reason = metric.Interpretation.Reason,
                },
            ContextCount = metric.Context?.Count ?? 0,
            Diagnostics = Array.AsReadOnly(diagnostics),
            Metadata = new ReadOnlyDictionary<string, string>(metadata),
        };
    }
}
