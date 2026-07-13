namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Controls whether a Langfuse dataset-run-item link failure prevents experiment item execution.
/// </summary>
public enum LangfuseExperimentItemLinkFailureMode
{
    /// <summary>
    /// Continue item execution and return a failed link status.
    /// </summary>
    BestEffort = 0,

    /// <summary>
    /// Propagate the link failure without invoking the item callback.
    /// </summary>
    Strict = 1,
}
