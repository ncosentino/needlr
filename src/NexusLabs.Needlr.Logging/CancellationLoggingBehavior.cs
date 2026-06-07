namespace NexusLabs.Needlr.Logging;

/// <summary>
/// Controls what a Needlr source-generated logging method does when its exception argument is a
/// cancellation (see <see cref="NeedlrCancellationLogging"/>).
/// </summary>
public enum CancellationLoggingBehavior
{
    /// <summary>
    /// Suppress the log entry entirely. This is the default — cancellation is normal control flow
    /// and is usually not worth logging.
    /// </summary>
    Skip = 0,

    /// <summary>
    /// Log normally at the method's declared level, as if the exception were not a cancellation.
    /// This effectively turns the cancellation guard off.
    /// </summary>
    Log = 1,

    /// <summary>
    /// Log the entry, but at the reduced level configured by
    /// <see cref="NeedlrCancellationLogging.DemotedLevel"/> instead of the method's declared level.
    /// </summary>
    Demote = 2,
}
