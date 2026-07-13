namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Contains the callback value and Langfuse publication status for one experiment item.
/// </summary>
/// <typeparam name="T">The callback result type.</typeparam>
public sealed class LangfuseExperimentItemResult<T>
{
    /// <summary>
    /// Initializes a new experiment item result.
    /// </summary>
    /// <param name="value">The value returned by the item callback.</param>
    /// <param name="traceId">The Langfuse trace id, or <see langword="null"/> when unavailable.</param>
    /// <param name="link">The dataset-run-item link outcome.</param>
    public LangfuseExperimentItemResult(
        T value,
        string? traceId,
        LangfuseExperimentItemLinkResult link)
    {
        ArgumentNullException.ThrowIfNull(link);
        Value = value;
        TraceId = traceId;
        Link = link;
    }

    /// <summary>
    /// Gets the value returned by the item callback.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets the Langfuse trace id, or <see langword="null"/> when tracing was disabled or unsampled.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Gets the outcome of linking the item trace to the Langfuse dataset run.
    /// </summary>
    public LangfuseExperimentItemLinkResult Link { get; }
}
