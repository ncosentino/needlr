using System.Text.Json.Serialization;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one isolated run-evaluation outcome.
/// </summary>
public sealed class ExperimentRunEvaluationResult
{
    /// <summary>Gets the stable evaluator name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the evaluator status.</summary>
    public required ExperimentRunEvaluationStatus Status { get; init; }

    /// <summary>Gets the mutable MEAI result when evaluation succeeded.</summary>
    [JsonIgnore]
    public EvaluationResult? Evaluation { get; init; }

    /// <summary>Gets immutable normalized metric snapshots.</summary>
    public IReadOnlyList<ExperimentMetricSnapshot> Metrics { get; init; } = [];

    /// <summary>Gets the structured failure when evaluation failed.</summary>
    public ExperimentFailure? Failure { get; init; }
}
