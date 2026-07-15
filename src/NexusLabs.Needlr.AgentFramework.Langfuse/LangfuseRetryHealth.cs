namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes bounded Langfuse REST retry attempts by cause.
/// </summary>
public sealed record LangfuseRetryHealth
{
    internal LangfuseRetryHealth(
        long rateLimited,
        long transientServer,
        long timedOut,
        long transport)
    {
        RateLimited = rateLimited;
        TransientServer = transientServer;
        TimedOut = timedOut;
        Transport = transport;
    }

    /// <summary>Gets retries caused by HTTP 429 responses.</summary>
    public long RateLimited { get; }

    /// <summary>Gets retries caused by transient HTTP 5xx responses.</summary>
    public long TransientServer { get; }

    /// <summary>Gets retries caused by request timeouts.</summary>
    public long TimedOut { get; }

    /// <summary>Gets retries caused by transport failures without a response.</summary>
    public long Transport { get; }

    /// <summary>Gets the total retry attempts.</summary>
    public long Total => RateLimited + TransientServer + TimedOut + Transport;
}
