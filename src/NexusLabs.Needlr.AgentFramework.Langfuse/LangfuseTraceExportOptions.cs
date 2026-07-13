namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures bounded local batching for Langfuse trace export.
/// </summary>
public sealed class LangfuseTraceExportOptions
{
    /// <summary>
    /// Gets or sets the maximum number of completed activities held locally. Defaults to 2048.
    /// </summary>
    public int MaxQueueSize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the maximum wait before a partial batch is exported. Defaults to five seconds.
    /// </summary>
    public TimeSpan ScheduledDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum activities sent in one OTLP request. Defaults to 512.
    /// </summary>
    public int MaxBatchSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the OTLP HTTP request timeout. Defaults to 30 seconds.
    /// </summary>
    /// <remarks>
    /// This bounds the exporter transport request. It does not prove that Langfuse durably
    /// processed or made the trace queryable.
    /// </remarks>
    public TimeSpan ExporterTimeout { get; set; } = TimeSpan.FromSeconds(30);

    internal int ScheduledDelayMilliseconds => ToMilliseconds(
        ScheduledDelay,
        nameof(ScheduledDelay));

    internal int ExporterTimeoutMilliseconds => ToMilliseconds(
        ExporterTimeout,
        nameof(ExporterTimeout));

    internal void Validate()
    {
        if (MaxQueueSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxQueueSize),
                MaxQueueSize,
                "The Langfuse trace export queue size must be at least one.");
        }

        if (MaxBatchSize < 1 || MaxBatchSize > MaxQueueSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxBatchSize),
                MaxBatchSize,
                "The Langfuse trace export batch size must be between one and the queue size.");
        }

        _ = ScheduledDelayMilliseconds;
        _ = ExporterTimeoutMilliseconds;
    }

    private static int ToMilliseconds(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero
            || value == Timeout.InfiniteTimeSpan
            || value.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The Langfuse trace export duration must be finite, positive, and no greater than Int32.MaxValue milliseconds.");
        }

        return (int)Math.Ceiling(value.TotalMilliseconds);
    }
}
