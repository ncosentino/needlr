using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Computes character counts of chat messages, responses, tool arguments, and tool
/// results for diagnostics capture. Character counts are a direct programmatic
/// measure of payload size — distinct from the LLM-provider-reported
/// <see cref="TokenUsage"/> — and are useful for detecting chat-reducer drift,
/// prompt bloat, and unexpectedly large tool responses during evaluation.
/// </summary>
/// <remarks>
/// All methods are null-safe and exception-tolerant: any failure to measure
/// returns <c>0</c> rather than throwing. This matches the diagnostics alpha
/// invariant that capture must never destabilize the live path.
/// </remarks>
public static class DiagnosticsCharCounter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Sums the character count of all text contents across the supplied chat
    /// messages. Non-text content (images, function calls) is ignored.
    /// </summary>
    public static long ChatMessagesLength(IEnumerable<ChatMessage>? messages)
    {
        if (messages is null)
        {
            return 0;
        }

        long total = 0;
        foreach (var message in messages)
        {
            total += SumTextContents(message.Contents);
        }
        return total;
    }

    /// <summary>
    /// Sums the character count of all text contents across every message in the
    /// supplied <see cref="ChatResponse"/>. Returns <c>0</c> when the response
    /// is <see langword="null"/> or contains no text content.
    /// </summary>
    public static long ChatResponseLength(ChatResponse? response)
    {
        if (response is null)
        {
            return 0;
        }

        return ChatMessagesLength(response.Messages);
    }

    /// <summary>
    /// Returns the character length of the JSON-serialized representation of
    /// the supplied value. Null-safe and exception-tolerant: returns <c>0</c>
    /// when the value is <see langword="null"/> or cannot be serialized.
    /// </summary>
    public static long JsonLength(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, _options);
            return json.Length;
        }
        catch
        {
            return 0;
        }
    }

    private static long SumTextContents(IList<AIContent>? contents)
    {
        if (contents is null || contents.Count == 0)
        {
            return 0;
        }

        long total = 0;
        foreach (var content in contents)
        {
            if (content is TextContent text && text.Text is not null)
            {
                total += text.Text.Length;
            }
        }
        return total;
    }
}
