namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Controls how the pipeline runner handles a failed stage result.
/// </summary>
public enum FailureDisposition
{
    /// <summary>Abort the pipeline immediately.</summary>
    AbortPipeline,

    /// <summary>
    /// Continue the pipeline. The failure is advisory and does not affect
    /// pipeline success.
    /// </summary>
    ContinueAdvisory,
}
