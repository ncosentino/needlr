namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Receives progress events as they occur during agent/workflow execution.
/// Consumers implement this to build SSE streams, console displays, trace diagrams, etc.
/// </summary>
public interface IProgressSink
{
    /// <summary>
    /// Called for each progress event. Implementations should be fast — a slow sink
    /// delays the agent pipeline (use <c>ChannelProgressReporter</c> for non-blocking delivery).
    /// </summary>
    ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken);
}
