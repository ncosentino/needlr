namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes the most recent local telemetry drain attempt.
/// </summary>
public enum LangfuseDrainStatus
{
    /// <summary>No local drain has been attempted.</summary>
    NotAttempted,

    /// <summary>A local drain is currently in progress.</summary>
    InProgress,

    /// <summary>The local providers reported that the requested drain completed.</summary>
    Completed,

    /// <summary>The local providers did not complete within the supplied budget.</summary>
    Incomplete,

    /// <summary>Telemetry export is disabled.</summary>
    Disabled,
}
