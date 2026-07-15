namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides one canonical quality result plus independent provider publication outcomes.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <typeparam name="TOutput">The caller-owned output type.</typeparam>
public sealed record ExperimentRunOutcome<TCase, TOutput>
{
    /// <summary>Gets the current canonical outcome schema version.</summary>
    public const int CurrentSchemaVersion = 4;

    /// <summary>Gets the canonical outcome schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets the canonical quality result.</summary>
    public required ExperimentRunResult<TCase, TOutput> Result { get; init; }

    /// <summary>Gets aggregate publication health across item scopes and final sinks.</summary>
    public required ExperimentPublicationStatus PublicationStatus { get; init; }

    /// <summary>Gets final sink results in registration order.</summary>
    public IReadOnlyList<ExperimentSinkResult> SinkResults { get; init; } = [];
}
