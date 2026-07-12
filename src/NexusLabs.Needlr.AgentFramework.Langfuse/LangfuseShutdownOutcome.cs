namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Reports whether local OpenTelemetry trace and metric provider shutdown completed.
/// </summary>
/// <remarks>
/// This outcome only describes local provider shutdown and drain. It does not guarantee that
/// Langfuse durably ingested the exported telemetry.
/// </remarks>
public sealed record LangfuseShutdownOutcome
{
    /// <summary>
    /// Initializes a shutdown outcome.
    /// </summary>
    /// <param name="isFinal">
    /// Whether this is the session's final cached outcome rather than an observation that another
    /// caller currently owns shutdown.
    /// </param>
    /// <param name="traces">The local trace-provider shutdown status.</param>
    /// <param name="metrics">The local metric-provider shutdown status.</param>
    public LangfuseShutdownOutcome(
        bool isFinal,
        LangfuseProviderShutdownStatus traces,
        LangfuseProviderShutdownStatus metrics)
    {
        IsFinal = isFinal;
        Traces = traces;
        Metrics = metrics;
    }

    /// <summary>
    /// Gets a value indicating whether this is the session's final cached shutdown outcome.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> means another caller currently owns final shutdown. In that case
    /// <see cref="Traces"/> and <see cref="Metrics"/> are
    /// <see cref="LangfuseProviderShutdownStatus.NotAttempted"/>.
    /// </remarks>
    public bool IsFinal { get; }

    /// <summary>
    /// Gets the local trace-provider shutdown status.
    /// </summary>
    public LangfuseProviderShutdownStatus Traces { get; }

    /// <summary>
    /// Gets the local metric-provider shutdown status.
    /// </summary>
    public LangfuseProviderShutdownStatus Metrics { get; }
}
