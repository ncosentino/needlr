namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Identifies a machine-actionable Langfuse publication failure category.
/// </summary>
public enum LangfusePublicationFailureCode
{
    /// <summary>
    /// Langfuse rejected the request or the transport failed.
    /// </summary>
    ApiRejected = 0,

    /// <summary>
    /// Langfuse returned an empty, malformed, or mismatched response.
    /// </summary>
    InvalidResponse = 1,

    /// <summary>
    /// Successful item links returned conflicting dataset-run ids.
    /// </summary>
    InconsistentDatasetRunIdentity = 2,

    /// <summary>
    /// No authoritative dataset-run id is available for a run-level score.
    /// </summary>
    DatasetRunIdentityUnavailable = 3,
}
