namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Receives callbacks when an <see cref="IProgressSink"/> throws while handling
/// an event. Without a handler, sink exceptions would be silently swallowed.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must be fast and must not throw — they run on the reporter's
/// delivery path (synchronous or channel consumer). Throwing from the handler
/// is undefined behavior and may tear down the consumer loop.
/// </para>
/// <para>
/// Register a custom implementation in DI to replace the default no-op handler.
/// Typical implementations forward to <c>ILogger</c>, Application Insights, or
/// an in-process diagnostic bus.
/// </para>
/// </remarks>
public interface IProgressReporterErrorHandler
{
    /// <summary>
    /// Called when <paramref name="sink"/> fails to process <paramref name="progressEvent"/>.
    /// </summary>
    /// <param name="sink">The sink that threw.</param>
    /// <param name="progressEvent">The event that could not be delivered.</param>
    /// <param name="exception">The exception thrown by the sink (already unwrapped from <see cref="AggregateException"/> where applicable).</param>
    void OnSinkException(IProgressSink sink, IProgressEvent progressEvent, Exception exception);
}
