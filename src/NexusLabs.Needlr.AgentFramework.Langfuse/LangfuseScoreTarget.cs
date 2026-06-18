namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Identifies the Langfuse object a score is attached to. Langfuse supports scoring a trace, a
/// specific observation within a trace, or a whole session (a score may also target a dataset run,
/// which the experiment path handles separately).
/// </summary>
internal readonly record struct LangfuseScoreTarget
{
    private LangfuseScoreTarget(string? traceId, string? observationId, string? sessionId, string contextId)
    {
        TraceId = traceId;
        ObservationId = observationId;
        SessionId = sessionId;
        ContextId = contextId;
    }

    /// <summary>Gets the trace id, or <see langword="null"/> for a session-only score.</summary>
    public string? TraceId { get; }

    /// <summary>Gets the observation id, or <see langword="null"/>.</summary>
    public string? ObservationId { get; }

    /// <summary>Gets the session id, or <see langword="null"/>.</summary>
    public string? SessionId { get; }

    /// <summary>Gets a human-readable id used in diagnostics when a score upload fails.</summary>
    public string ContextId { get; }

    /// <summary>Targets an entire trace.</summary>
    /// <param name="traceId">The trace id.</param>
    /// <returns>A trace-level target.</returns>
    public static LangfuseScoreTarget Trace(string traceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        return new LangfuseScoreTarget(traceId, observationId: null, sessionId: null, traceId);
    }

    /// <summary>Targets a specific observation within a trace.</summary>
    /// <param name="traceId">The owning trace id.</param>
    /// <param name="observationId">The observation id.</param>
    /// <returns>An observation-level target.</returns>
    public static LangfuseScoreTarget Observation(string traceId, string observationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationId);
        return new LangfuseScoreTarget(traceId, observationId, sessionId: null, observationId);
    }

    /// <summary>Targets a whole session.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <returns>A session-level target.</returns>
    public static LangfuseScoreTarget Session(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return new LangfuseScoreTarget(traceId: null, observationId: null, sessionId, sessionId);
    }
}
