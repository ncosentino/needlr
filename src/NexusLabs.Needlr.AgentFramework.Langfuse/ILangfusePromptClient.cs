namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Fetches and creates prompts in Langfuse prompt management, so the prompt an eval runs against
/// lives in Langfuse and the generations link to the exact version that was used.
/// </summary>
public interface ILangfusePromptClient
{
    /// <summary>
    /// Gets a value indicating whether prompt operations are performed. <see langword="false"/> when
    /// Langfuse is not configured, in which case <see cref="GetPromptAsync"/> returns
    /// <see langword="null"/>.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Fetches a managed prompt by name, optionally pinned to a label or version. With neither, the
    /// <c>production</c> label is used. Returns <see langword="null"/> when the prompt does not exist
    /// (or Langfuse is not configured).
    /// </summary>
    /// <param name="name">The prompt name. URL-encode folder paths.</param>
    /// <param name="label">An optional label to resolve (for example <c>production</c>).</param>
    /// <param name="version">An optional specific version. Takes precedence over <paramref name="label"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The prompt, or <see langword="null"/>.</returns>
    Task<LangfusePrompt?> GetPromptAsync(string name, string? label = null, int? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new version of a text prompt (creating the prompt if the name is new).
    /// </summary>
    /// <param name="name">The prompt name.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="labels">Optional labels to apply (for example <c>production</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created prompt version.</returns>
    Task<LangfusePrompt> CreateTextPromptAsync(string name, string prompt, IEnumerable<string>? labels = null, CancellationToken cancellationToken = default);
}
