namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes whether a Langfuse experiment run has one authoritative dataset-run id.
/// </summary>
public enum LangfuseDatasetRunIdentityStatus
{
    /// <summary>
    /// No successful item link has returned a dataset-run id.
    /// </summary>
    Unresolved = 0,

    /// <summary>
    /// Successful item links agree on one dataset-run id.
    /// </summary>
    Resolved = 1,

    /// <summary>
    /// Successful item links returned conflicting dataset-run ids.
    /// </summary>
    Inconsistent = 2,

    /// <summary>
    /// Langfuse is disabled and no remote identity will be created.
    /// </summary>
    Disabled = 3,
}
