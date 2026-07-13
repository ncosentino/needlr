namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Internal result for one score projection.
/// </summary>
internal sealed class LangfuseScoreRecordResult
{
    public LangfuseScoreRecordResult(
        string name,
        LangfuseScoreRecordStatus status,
        LangfuseException? failure)
    {
        Name = name;
        Status = status;
        Failure = failure;
    }

    public string Name { get; }

    public LangfuseScoreRecordStatus Status { get; }

    public LangfuseException? Failure { get; }
}
