namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Provides ambient access to the <see cref="IProgressReporter"/> for the current
/// async flow. Follows the <c>IHttpContextAccessor</c> pattern — backed by
/// <see cref="AsyncLocal{T}"/> so concurrent orchestrations see their own reporters.
/// </summary>
/// <remarks>
/// <para>
/// Orchestrators set the reporter via <see cref="BeginScope"/> before running a workflow.
/// Middleware (chat client, function calling) reads <see cref="Current"/> to emit events
/// in real-time without needing the reporter passed as a parameter.
/// </para>
/// </remarks>
public interface IProgressReporterAccessor
{
    /// <summary>
    /// Gets the progress reporter for the current async flow, or
    /// <see cref="NullProgressReporter.Instance"/> if no scope is active.
    /// </summary>
    IProgressReporter Current { get; }

    /// <summary>
    /// Sets the progress reporter for the current async flow.
    /// Disposing the returned handle restores the previous reporter.
    /// </summary>
    IDisposable BeginScope(IProgressReporter reporter);
}
