namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Contains immutable counts of experiment item-link outcomes.
/// </summary>
public sealed class LangfuseExperimentItemLinkCounts
{
    /// <summary>
    /// Initializes item-link counts.
    /// </summary>
    /// <param name="linked">The number of accepted item links.</param>
    /// <param name="failed">The number of failed item links.</param>
    /// <param name="inconsistent">The number of links exposing inconsistent run identity.</param>
    /// <param name="notSampled">The number of items without a sampled trace.</param>
    /// <param name="disabled">The number of disabled-mode item links.</param>
    public LangfuseExperimentItemLinkCounts(
        int linked,
        int failed,
        int inconsistent,
        int notSampled,
        int disabled)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(linked);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);
        ArgumentOutOfRangeException.ThrowIfNegative(inconsistent);
        ArgumentOutOfRangeException.ThrowIfNegative(notSampled);
        ArgumentOutOfRangeException.ThrowIfNegative(disabled);

        Linked = linked;
        Failed = failed;
        Inconsistent = inconsistent;
        NotSampled = notSampled;
        Disabled = disabled;
    }

    /// <summary>Gets the number of accepted item links.</summary>
    public int Linked { get; }

    /// <summary>Gets the number of failed item links.</summary>
    public int Failed { get; }

    /// <summary>Gets the number of item links exposing inconsistent run identity.</summary>
    public int Inconsistent { get; }

    /// <summary>Gets the number of items without a sampled trace to link.</summary>
    public int NotSampled { get; }

    /// <summary>Gets the number of disabled-mode item links.</summary>
    public int Disabled { get; }

    /// <summary>Gets the total number of observed item-link outcomes.</summary>
    public int Total => Linked + Failed + Inconsistent + NotSampled + Disabled;
}
