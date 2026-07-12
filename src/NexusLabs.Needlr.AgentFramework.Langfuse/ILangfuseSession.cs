namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Represents a standalone Langfuse export owner. Extends the non-owning
/// <see cref="ILangfuseClient"/> facade with explicit flush, shutdown, and disposal lifecycle.
/// </summary>
/// <remarks>
/// Obtain an instance from <see cref="LangfuseTelemetry.Start(LangfuseOptions)"/>. Keep it alive
/// for the lifetime over which agent runs and evaluations should be captured, then dispose it to
/// perform a bounded, best-effort final drain.
/// </remarks>
public interface ILangfuseSession : ILangfuseClient, IDisposable
{
    /// <summary>
    /// Flushes buffered telemetry to Langfuse.
    /// </summary>
    /// <param name="timeout">
    /// Maximum time to wait, or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to wait
    /// indefinitely. A provider default is used when <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if the flush succeeded; otherwise <see langword="false"/>.</returns>
    bool Flush(TimeSpan? timeout = null);

    /// <summary>
    /// Performs final local OpenTelemetry provider shutdown and releases all resources owned by the
    /// session.
    /// </summary>
    /// <param name="timeout">
    /// The total timeout budget shared by trace and metric provider shutdown, or
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to explicitly wait indefinitely.
    /// </param>
    /// <returns>
    /// An outcome describing whether local trace and metric provider shutdown completed.
    /// </returns>
    /// <remarks>
    /// Exactly one caller performs final shutdown. Concurrent callers return a non-final outcome,
    /// and calls made after completion return the cached final outcome.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeout"/> is negative and is not
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.
    /// </exception>
    LangfuseShutdownOutcome Shutdown(TimeSpan timeout);
}
