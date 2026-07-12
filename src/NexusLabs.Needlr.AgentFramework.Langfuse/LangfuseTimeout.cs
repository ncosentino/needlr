namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Converts Langfuse timeout contracts to the millisecond representation used by OpenTelemetry.
/// </summary>
internal static class LangfuseTimeout
{
    public static int ToFlushMilliseconds(TimeSpan? timeout)
    {
        if (timeout is null || timeout.Value == Timeout.InfiniteTimeSpan)
        {
            return Timeout.Infinite;
        }

        var milliseconds = (long)timeout.Value.TotalMilliseconds;
        return milliseconds < 0
            ? Timeout.Infinite
            : (int)Math.Min(milliseconds, int.MaxValue);
    }

    public static int ToShutdownMilliseconds(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return Timeout.Infinite;
        }

        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                $"Shutdown timeout must be non-negative or {nameof(Timeout)}.{nameof(Timeout.InfiniteTimeSpan)}.");
        }

        return (int)Math.Min(
            Math.Ceiling(timeout.TotalMilliseconds),
            int.MaxValue);
    }
}
