namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfusePromptClient"/> backed by the shared <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed class LangfusePromptClient : ILangfusePromptClient
{
    private readonly LangfuseApiClient _apiClient;

    public LangfusePromptClient(LangfuseApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task<LangfusePrompt?> GetPromptAsync(string name, string? label = null, int? version = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var query = version is { } v
            ? $"?version={v}"
            : !string.IsNullOrWhiteSpace(label) ? $"?label={Uri.EscapeDataString(label)}" : string.Empty;

        var dto = await _apiClient
            .GetOrDefaultAsync<LangfusePromptDto>($"api/public/v2/prompts/{Uri.EscapeDataString(name)}{query}", cancellationToken)
            .ConfigureAwait(false);

        return dto?.ToPrompt();
    }

    /// <inheritdoc />
    public async Task<LangfusePrompt> CreateTextPromptAsync(string name, string prompt, IEnumerable<string>? labels = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var request = new LangfuseCreatePromptRequest
        {
            Name = name,
            Type = "text",
            Prompt = prompt,
            Labels = labels?.ToList(),
        };

        var dto = await _apiClient
            .PostAsync<LangfuseCreatePromptRequest, LangfusePromptDto>("api/public/v2/prompts", request, cancellationToken)
            .ConfigureAwait(false);

        return dto?.ToPrompt()
            ?? throw new LangfuseException($"Langfuse returned no body when creating prompt '{name}'.");
    }
}
