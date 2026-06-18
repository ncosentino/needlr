namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable payload for a single Langfuse score ingested via the public Scores API.
/// </summary>
/// <remarks>
/// Property names are projected to camelCase by the score client to match the Langfuse REST
/// contract. <see cref="Value"/> is a <see cref="double"/> for numeric and boolean scores and a
/// <see cref="string"/> for categorical and text scores.
/// </remarks>
internal sealed record LangfuseScore
{
    /// <summary>
    /// Gets the id of the trace the score is attached to. Optional: a score may instead target a
    /// session (<see cref="SessionId"/>) or dataset run (<see cref="DatasetRunId"/>). Required when
    /// <see cref="ObservationId"/> is set.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>Gets the score name (for example an evaluator metric name). Required.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the score value: a number for numeric/boolean, a string for categorical/text.</summary>
    public required object Value { get; init; }

    /// <summary>Gets the Langfuse data type token (<c>NUMERIC</c>, <c>BOOLEAN</c>, <c>CATEGORICAL</c>, <c>TEXT</c>).</summary>
    public required string DataType { get; init; }

    /// <summary>Gets the optional id of a specific observation within the trace to attach the score to.</summary>
    public string? ObservationId { get; init; }

    /// <summary>Gets the optional id of a session to attach the score to (session-level scoring).</summary>
    public string? SessionId { get; init; }

    /// <summary>Gets the optional id of a dataset run to attach the score to (experiment-level scoring).</summary>
    public string? DatasetRunId { get; init; }

    /// <summary>Gets the optional free-text comment that accompanies the score.</summary>
    public string? Comment { get; init; }

    /// <summary>Gets the optional client-supplied id used as an idempotency key.</summary>
    public string? Id { get; init; }
}
