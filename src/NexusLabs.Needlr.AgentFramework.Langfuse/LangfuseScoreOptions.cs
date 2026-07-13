namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures one score publication to Langfuse.
/// </summary>
public sealed class LangfuseScoreOptions
{
    /// <summary>
    /// Gets or sets the optional stable Langfuse score id.
    /// </summary>
    /// <remarks>
    /// Langfuse uses this value as an idempotency key together with the score name and its
    /// server-assigned UTC date. Repeating a complete score with the same id and name replaces it
    /// only when both requests fall on the same UTC date; a retry across that boundary can create
    /// another score.
    /// </remarks>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the optional free-text explanation attached to the score.
    /// </summary>
    public string? Comment { get; set; }
}
