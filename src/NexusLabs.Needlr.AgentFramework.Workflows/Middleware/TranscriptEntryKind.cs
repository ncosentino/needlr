namespace NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

/// <summary>
/// Indicates whether a <see cref="TranscriptEntry"/> represents a request
/// sent to the LLM or a response received from it.
/// </summary>
public enum TranscriptEntryKind
{
    /// <summary>
    /// The entry records messages and options sent to the LLM.
    /// </summary>
    Request,

    /// <summary>
    /// The entry records a response received from the LLM.
    /// </summary>
    Response,
}
