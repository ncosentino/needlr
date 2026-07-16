namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes the direct API outcome for one experiment score.
/// </summary>
public enum LangfuseExperimentScoreStatus
{
    /// <summary>
    /// Langfuse accepted the score request.
    /// </summary>
    Accepted = 0,

    /// <summary>
    /// The score request failed in nonfatal mode.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// No authoritative score target was available, so no request was sent.
    /// </summary>
    NotAttempted = 2,

    /// <summary>
    /// The evaluation metric had no publishable value.
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// Langfuse was disabled, so no request was sent.
    /// </summary>
    Disabled = 4,
}
