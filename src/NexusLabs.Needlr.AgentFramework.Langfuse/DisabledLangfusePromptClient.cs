namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfusePromptClient"/> returned when Langfuse is not configured.
/// <see cref="GetPromptAsync"/> returns <see langword="null"/>; creating a prompt is not possible
/// without a configured target and throws.
/// </summary>
internal sealed class DisabledLangfusePromptClient : ILangfusePromptClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task<LangfusePrompt?> GetPromptAsync(string name, string? label = null, int? version = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<LangfusePrompt?>(null);

    /// <inheritdoc />
    public Task<LangfusePrompt> CreateTextPromptAsync(string name, string prompt, IEnumerable<string>? labels = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Langfuse is not configured, so a prompt cannot be created. Provide credentials and an explicit Host/Region.");
}
