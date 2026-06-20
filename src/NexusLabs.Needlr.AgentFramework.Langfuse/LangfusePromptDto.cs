using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Wire shape for a prompt returned by the Langfuse prompt-management API. Projected to the public
/// <see cref="LangfusePrompt"/>.
/// </summary>
internal sealed record LangfusePromptDto
{
    public string Name { get; init; } = string.Empty;

    public int Version { get; init; }

    public string Type { get; init; } = "text";

    public JsonElement Prompt { get; init; }

    public IReadOnlyList<string>? Labels { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public LangfusePrompt ToPrompt() => new()
    {
        Name = Name,
        Version = Version,
        Type = Type,
        Prompt = Prompt,
        Labels = Labels ?? [],
        Tags = Tags ?? [],
    };
}
