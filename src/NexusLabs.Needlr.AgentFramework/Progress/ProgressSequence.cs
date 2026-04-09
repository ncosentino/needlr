namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Global sequence counter for progress events. Ensures all events across
/// all reporters are globally ordered.
/// </summary>
public static class ProgressSequence
{
    private static long _counter;

    /// <summary>Allocates the next globally-ordered sequence number.</summary>
    public static long Next() => Interlocked.Increment(ref _counter);
}
