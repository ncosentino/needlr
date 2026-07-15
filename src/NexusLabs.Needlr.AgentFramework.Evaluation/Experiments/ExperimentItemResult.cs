using System.Text.Json.Serialization;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Describes one case trial, including its complete attempt history and terminal output.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed class ExperimentItemResult<TCase, TOutput>
{
    /// <summary>Gets the zero-based stable sequence.</summary>
    public required int Sequence { get; init; }

    /// <summary>Gets the materialized case.</summary>
    public required ExperimentCase<TCase> Case { get; init; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public required int TrialIndex { get; init; }

    /// <summary>Gets the terminal item status.</summary>
    public required ExperimentItemStatus Status { get; init; }

    /// <summary>Gets every operational attempt in order.</summary>
    public required IReadOnlyList<ExperimentAttemptResult> Attempts { get; init; }

    /// <summary>Gets a value indicating whether <see cref="Output"/> contains a task output.</summary>
    public required bool HasOutput { get; init; }

    /// <summary>Gets the terminal successful output, when available.</summary>
    public TOutput? Output { get; init; }

    /// <summary>Gets the mutable MEAI evaluation result, when evaluation succeeded.</summary>
    [JsonIgnore]
    public EvaluationResult? Evaluation { get; init; }

    /// <summary>Gets immutable normalized metric snapshots.</summary>
    public IReadOnlyList<ExperimentMetricSnapshot> Metrics { get; init; } = [];

    /// <summary>Gets namespaced provider identifiers in item-scope registration order.</summary>
    public IReadOnlyList<ExperimentItemCorrelation> Correlations { get; init; } = [];

    /// <summary>Gets item-scope publication results in registration order.</summary>
    public IReadOnlyList<ExperimentItemPublicationResult> Publications { get; init; } = [];

    /// <summary>Gets the structured terminal failure, when present.</summary>
    public ExperimentFailure? Failure { get; init; }
}
