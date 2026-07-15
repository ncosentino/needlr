namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Internal result for one score projection.
/// </summary>
internal sealed record LangfuseScoreRecordResult
{
    public LangfuseScoreRecordResult(
        string? scoreId,
        string name,
        LangfuseScoreRecordStatus status,
        LangfuseException? failure)
    {
        ScoreId = scoreId;
        Name = name;
        Status = status;
        Failure = failure;
    }

    public string? ScoreId { get; }

    public string Name { get; }

    public LangfuseScoreRecordStatus Status { get; }

    public LangfuseException? Failure { get; }
}
