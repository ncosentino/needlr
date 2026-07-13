namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes direct REST publication outcomes for one operation category.
/// </summary>
public sealed class LangfusePublicationOperationHealth
{
    internal LangfusePublicationOperationHealth(
        long inFlight,
        long succeeded,
        long failed,
        long canceled)
    {
        InFlight = inFlight;
        Succeeded = succeeded;
        Failed = failed;
        Canceled = canceled;
    }

    /// <summary>Gets the operations currently awaiting a terminal result.</summary>
    public long InFlight { get; }

    /// <summary>Gets the operations accepted by the Langfuse REST API.</summary>
    public long Succeeded { get; }

    /// <summary>Gets the operations that terminated with a publication failure.</summary>
    public long Failed { get; }

    /// <summary>Gets the operations canceled by their caller.</summary>
    public long Canceled { get; }

    /// <summary>Gets the total terminal operations.</summary>
    public long Total => Succeeded + Failed + Canceled;
}
