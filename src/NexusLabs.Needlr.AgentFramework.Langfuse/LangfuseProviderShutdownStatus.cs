namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes the local OpenTelemetry shutdown state for one telemetry provider.
/// </summary>
public enum LangfuseProviderShutdownStatus
{
    /// <summary>
    /// The provider completed its local shutdown and drain within the supplied timeout budget.
    /// </summary>
    Completed,

    /// <summary>
    /// The provider did not complete its local shutdown and drain within the supplied timeout
    /// budget, or OpenTelemetry otherwise reported shutdown failure.
    /// </summary>
    Incomplete,

    /// <summary>
    /// The provider was not configured for this session.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// This call observed another caller performing shutdown, so it did not attempt provider
    /// shutdown and returned immediately.
    /// </summary>
    NotAttempted,
}
