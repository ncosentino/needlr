namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes whether an experiment item trace was linked to its Langfuse dataset run.
/// </summary>
public enum LangfuseExperimentItemLinkStatus
{
    /// <summary>
    /// The trace was linked to the dataset run.
    /// </summary>
    Linked = 0,

    /// <summary>
    /// Langfuse rejected or failed the link, and best-effort mode continued item execution.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// No sampled scenario trace was available to link.
    /// </summary>
    NotSampled = 2,

    /// <summary>
    /// Langfuse was disabled, so no link was attempted.
    /// </summary>
    Disabled = 3,
}
