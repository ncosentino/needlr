namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides the canonical ordered result for one provider-neutral experiment run.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed class ExperimentRunResult<TCase, TOutput>
{
    /// <summary>Gets the current canonical result schema version.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Gets the canonical result schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets the experiment name.</summary>
    public required string ExperimentName { get; init; }

    /// <summary>Gets the materialized source identity.</summary>
    public required ExperimentSourceReference Source { get; init; }

    /// <summary>Gets the UTC run start time.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets the elapsed run duration.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Gets the configured maximum active attempt count.</summary>
    public required int MaxConcurrency { get; init; }

    /// <summary>Gets the fixed number of worker tasks used by this run.</summary>
    public required int WorkerCount { get; init; }

    /// <summary>Gets item results in stable source/trial sequence order.</summary>
    public required IReadOnlyList<ExperimentItemResult<TCase, TOutput>> Items { get; init; }

    /// <summary>Gets isolated run-evaluation results in registration order.</summary>
    public IReadOnlyList<ExperimentRunEvaluationResult> RunEvaluations { get; init; } = [];

    /// <summary>Gets isolated policy results in registration order.</summary>
    public IReadOnlyList<ExperimentPolicyResult> PolicyResults { get; init; } = [];

    /// <summary>Gets the deterministic aggregate decision from required policies.</summary>
    public ExperimentRunDecision Decision { get; init; } = ExperimentRunDecision.NotEvaluated;
}
