namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Internal terminal outcome for one score projection.
/// </summary>
internal enum LangfuseScoreRecordStatus
{
    Accepted = 0,
    Failed = 1,
    Skipped = 2,
}
