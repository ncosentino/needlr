namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Identifies why a Langfuse REST operation was retried.
/// </summary>
internal enum LangfuseRetryReason
{
    RateLimited,
    TransientServer,
    TimedOut,
    Transport,
}
