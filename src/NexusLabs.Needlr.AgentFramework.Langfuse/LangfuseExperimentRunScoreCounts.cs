namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Contains immutable counts of dataset-run score outcomes.
/// </summary>
public sealed record LangfuseExperimentRunScoreCounts
{
    /// <summary>
    /// Initializes dataset-run score counts.
    /// </summary>
    /// <param name="accepted">The number of accepted score requests.</param>
    /// <param name="failed">The number of failed score requests.</param>
    /// <param name="notAttempted">The number of scores without an authoritative target.</param>
    /// <param name="skipped">The number of metrics without publishable values.</param>
    /// <param name="disabled">The number of disabled-mode scores.</param>
    public LangfuseExperimentRunScoreCounts(
        int accepted,
        int failed,
        int notAttempted,
        int skipped,
        int disabled)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accepted);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);
        ArgumentOutOfRangeException.ThrowIfNegative(notAttempted);
        ArgumentOutOfRangeException.ThrowIfNegative(skipped);
        ArgumentOutOfRangeException.ThrowIfNegative(disabled);

        Accepted = accepted;
        Failed = failed;
        NotAttempted = notAttempted;
        Skipped = skipped;
        Disabled = disabled;
    }

    /// <summary>Gets the number of accepted score requests.</summary>
    public int Accepted { get; }

    /// <summary>Gets the number of failed score requests.</summary>
    public int Failed { get; }

    /// <summary>Gets the number of scores not attempted because run identity was unavailable.</summary>
    public int NotAttempted { get; }

    /// <summary>Gets the number of evaluation metrics without publishable values.</summary>
    public int Skipped { get; }

    /// <summary>Gets the number of disabled-mode scores.</summary>
    public int Disabled { get; }

    /// <summary>Gets the total number of observed score outcomes.</summary>
    public int Total => Accepted + Failed + NotAttempted + Skipped + Disabled;
}
