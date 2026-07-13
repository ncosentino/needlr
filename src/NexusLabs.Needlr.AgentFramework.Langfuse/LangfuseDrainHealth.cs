namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes the latest local flush or shutdown drain.
/// </summary>
public sealed class LangfuseDrainHealth
{
    internal LangfuseDrainHealth(
        long attempts,
        LangfuseDrainStatus status,
        TimeSpan? duration)
    {
        Attempts = attempts;
        Status = status;
        Duration = duration;
    }

    /// <summary>Gets the number of local drain attempts.</summary>
    public long Attempts { get; }

    /// <summary>Gets the most recent local drain status.</summary>
    public LangfuseDrainStatus Status { get; }

    /// <summary>Gets the most recent drain duration, when an attempt completed.</summary>
    public TimeSpan? Duration { get; }
}
