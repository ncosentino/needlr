namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Applies a session's <see cref="LangfuseScoreFailureMode"/> to a failed score upload and tracks
/// how many uploads have failed. Shared by every scenario created from one session.
/// </summary>
internal sealed class LangfuseScoreFailureSink
{
    private readonly LangfuseScoreFailureMode _mode;
    private readonly Action<LangfuseScoreError>? _callback;
    private int _failedCount;

    public LangfuseScoreFailureSink(
        LangfuseScoreFailureMode mode,
        Action<LangfuseScoreError>? callback)
    {
        _mode = mode;
        _callback = callback;
    }

    /// <summary>Gets the cumulative number of score uploads that have failed.</summary>
    public int FailedCount => Volatile.Read(ref _failedCount);

    /// <summary>
    /// Records a failed score upload. In <see cref="LangfuseScoreFailureMode.Strict"/> mode the
    /// originating exception is rethrown; otherwise the failure counter is incremented, the error
    /// callback (if any) is invoked, and control returns to the caller.
    /// </summary>
    /// <param name="scoreName">The name of the score that failed.</param>
    /// <param name="traceId">The destination trace id, if known.</param>
    /// <param name="exception">The failure.</param>
    public void Record(string scoreName, string? traceId, LangfuseException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_mode == LangfuseScoreFailureMode.Strict)
        {
            throw exception;
        }

        Interlocked.Increment(ref _failedCount);
        _callback?.Invoke(new LangfuseScoreError(scoreName, traceId, exception));
    }
}
