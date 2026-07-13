namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Identifies whether repeating an HTTP operation preserves its provider-level semantics.
/// </summary>
internal enum LangfuseHttpRetryMode
{
    None,
    Idempotent,
}
