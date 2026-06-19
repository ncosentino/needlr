using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// A prompt fetched from Langfuse prompt management. Use <see cref="Text"/> for a text prompt's
/// content, and pass the prompt to <see cref="ILangfuseScenario.SetPrompt(LangfusePrompt)"/> to link
/// generations to the exact version that was used.
/// </summary>
public sealed record LangfusePrompt
{
    /// <summary>Gets the prompt name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the prompt version.</summary>
    public required int Version { get; init; }

    /// <summary>Gets the prompt type (<c>text</c> or <c>chat</c>).</summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the raw prompt content: a JSON string for a text prompt, or a JSON array of messages for
    /// a chat prompt.
    /// </summary>
    public JsonElement Prompt { get; init; }

    /// <summary>Gets the labels applied to this version (for example <c>production</c>).</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Gets the prompt tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the text content for a <c>text</c> prompt, or <see langword="null"/> for a chat prompt
    /// (use <see cref="Prompt"/> for chat messages).
    /// </summary>
    public string? Text =>
        string.Equals(Type, "text", StringComparison.OrdinalIgnoreCase) && Prompt.ValueKind == JsonValueKind.String
            ? Prompt.GetString()
            : null;
}
