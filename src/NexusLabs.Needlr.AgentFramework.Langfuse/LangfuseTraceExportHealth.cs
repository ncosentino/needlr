namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes local trace queue and exporter outcomes.
/// </summary>
public sealed class LangfuseTraceExportHealth
{
    internal LangfuseTraceExportHealth(
        long locallyObserved,
        long locallyEnqueued,
        long dropped,
        long acknowledged,
        long failed,
        long successfulBatches,
        long failedBatches)
    {
        LocallyObserved = locallyObserved;
        LocallyEnqueued = locallyEnqueued;
        Dropped = dropped;
        Acknowledged = acknowledged;
        Failed = failed;
        SuccessfulBatches = successfulBatches;
        FailedBatches = failedBatches;
    }

    /// <summary>Gets the recorded activities observed by the local processor.</summary>
    public long LocallyObserved { get; }

    /// <summary>Gets the activities accepted into the bounded local queue.</summary>
    public long LocallyEnqueued { get; }

    /// <summary>Gets the activities rejected because the local queue was saturated or closed.</summary>
    public long Dropped { get; }

    /// <summary>Gets the activities in batches acknowledged successfully by the OTLP exporter.</summary>
    public long Acknowledged { get; }

    /// <summary>Gets the activities removed from the queue in failed export batches.</summary>
    public long Failed { get; }

    /// <summary>Gets the number of exporter-acknowledged batches.</summary>
    public long SuccessfulBatches { get; }

    /// <summary>Gets the number of failed export batches.</summary>
    public long FailedBatches { get; }
}
