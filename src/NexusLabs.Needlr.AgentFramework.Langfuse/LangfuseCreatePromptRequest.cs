namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable payload for creating a text prompt (<c>POST /api/public/v2/prompts</c>). Property
/// names are projected to camelCase by <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed record LangfuseCreatePromptRequest
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Prompt { get; init; }

    public IReadOnlyList<string>? Labels { get; init; }
}
