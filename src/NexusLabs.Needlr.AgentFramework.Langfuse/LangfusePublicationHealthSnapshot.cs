namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Provides an immutable point-in-time view of Langfuse publication health.
/// </summary>
/// <remarks>
/// Export acknowledgement and drain completion describe local/transport outcomes only. They do not
/// prove that traces or scores are durably processed or queryable in Langfuse.
/// </remarks>
public sealed record LangfusePublicationHealthSnapshot
{
    internal LangfusePublicationHealthSnapshot(
        bool isEnabled,
        LangfuseTraceExportHealth traceExport,
        LangfusePublicationOperationHealth scoreUploads,
        LangfusePublicationOperationHealth itemLinks,
        LangfuseRetryHealth retries,
        LangfuseDrainHealth drain)
    {
        IsEnabled = isEnabled;
        TraceExport = traceExport;
        ScoreUploads = scoreUploads;
        ItemLinks = itemLinks;
        Retries = retries;
        Drain = drain;
    }

    /// <summary>Gets a value indicating whether publication is enabled.</summary>
    public bool IsEnabled { get; }

    /// <summary>Gets local trace queue and exporter health.</summary>
    public LangfuseTraceExportHealth TraceExport { get; }

    /// <summary>Gets direct score-upload health.</summary>
    public LangfusePublicationOperationHealth ScoreUploads { get; }

    /// <summary>Gets direct dataset-run-item link health.</summary>
    public LangfusePublicationOperationHealth ItemLinks { get; }

    /// <summary>Gets REST retry health.</summary>
    public LangfuseRetryHealth Retries { get; }

    /// <summary>Gets the latest local drain health.</summary>
    public LangfuseDrainHealth Drain { get; }
}
