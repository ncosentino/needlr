namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Identifies the Langfuse object a score is attached to. Langfuse supports scoring a trace, a
/// specific observation within a trace, a whole session, or a dataset run.
/// </summary>
internal readonly record struct LangfuseScoreTarget
{
    private LangfuseScoreTarget(
        string? traceId,
        string? observationId,
        string? sessionId,
        string? datasetRunId,
        string contextId)
    {
        TraceId = traceId;
        ObservationId = observationId;
        SessionId = sessionId;
        DatasetRunId = datasetRunId;
        ContextId = contextId;
    }

    /// <summary>
    /// Gets the trace id, or <see langword="null"/> for a session- or dataset-run-only score.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>Gets the observation id, or <see langword="null"/>.</summary>
    public string? ObservationId { get; }

    /// <summary>Gets the session id, or <see langword="null"/>.</summary>
    public string? SessionId { get; }

    /// <summary>Gets the dataset-run id, or <see langword="null"/>.</summary>
    public string? DatasetRunId { get; }

    /// <summary>Gets a human-readable id used in diagnostics when a score upload fails.</summary>
    public string ContextId { get; }

    /// <summary>Targets an entire trace.</summary>
    /// <param name="traceId">The trace id.</param>
    /// <returns>A trace-level target.</returns>
    public static LangfuseScoreTarget Trace(string traceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        return new LangfuseScoreTarget(
            traceId,
            observationId: null,
            sessionId: null,
            datasetRunId: null,
            traceId);
    }

    /// <summary>Targets a specific observation within a trace.</summary>
    /// <param name="traceId">The owning trace id.</param>
    /// <param name="observationId">The observation id.</param>
    /// <returns>An observation-level target.</returns>
    public static LangfuseScoreTarget Observation(string traceId, string observationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationId);
        return new LangfuseScoreTarget(
            traceId,
            observationId,
            sessionId: null,
            datasetRunId: null,
            observationId);
    }

    /// <summary>Targets a whole session.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <returns>A session-level target.</returns>
    public static LangfuseScoreTarget Session(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return new LangfuseScoreTarget(
            traceId: null,
            observationId: null,
            sessionId,
            datasetRunId: null,
            sessionId);
    }

    /// <summary>Targets a dataset run.</summary>
    /// <param name="datasetRunId">The dataset-run id.</param>
    /// <returns>A dataset-run-level target.</returns>
    public static LangfuseScoreTarget DatasetRun(string datasetRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetRunId);
        return new LangfuseScoreTarget(
            traceId: null,
            observationId: null,
            sessionId: null,
            datasetRunId,
            datasetRunId);
    }
}
