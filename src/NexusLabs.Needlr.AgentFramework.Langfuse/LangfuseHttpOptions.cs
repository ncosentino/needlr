namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures bounded timeout and retry behavior for Langfuse REST API requests.
/// </summary>
public sealed record LangfuseHttpOptions
{
    /// <summary>
    /// Gets or sets the timeout for each individual HTTP attempt. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum total attempts for retry-safe operations. Defaults to three.
    /// </summary>
    /// <remarks>
    /// A value of one disables retries. Needlr never retries writes that lack provider-supported
    /// idempotency.
    /// </remarks>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial exponential retry delay. Defaults to 200 milliseconds.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum delay between attempts, including delays derived from
    /// <c>Retry-After</c>. Defaults to five seconds.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        if (RequestTimeout <= TimeSpan.Zero || RequestTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RequestTimeout),
                RequestTimeout,
                "The Langfuse HTTP request timeout must be finite and greater than zero.");
        }

        if (MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxAttempts),
                MaxAttempts,
                "The Langfuse HTTP maximum attempt count must be at least one.");
        }

        if (InitialRetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialRetryDelay),
                InitialRetryDelay,
                "The Langfuse HTTP initial retry delay cannot be negative.");
        }

        if (MaxRetryDelay < InitialRetryDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRetryDelay),
                MaxRetryDelay,
                "The Langfuse HTTP maximum retry delay cannot be less than the initial retry delay.");
        }
    }
}
