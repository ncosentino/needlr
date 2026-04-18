using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Serialization-friendly projection of a <see cref="ChatResponse"/> used by
/// <see cref="FileEvaluationCaptureStore"/>. Only textual content is captured;
/// binary content, tool-call payloads, and provider-specific metadata are not
/// round-tripped.
/// </summary>
internal sealed class CapturedChatResponsePayload
{
    public string? ResponseId { get; set; }
    public string? ModelId { get; set; }
    public List<CapturedMessage> Messages { get; set; } = new();

    public static CapturedChatResponsePayload FromChatResponse(ChatResponse response)
    {
        var messages = new List<CapturedMessage>(response.Messages.Count);
        foreach (var message in response.Messages)
        {
            messages.Add(new CapturedMessage
            {
                Role = message.Role.Value,
                Text = message.Text,
            });
        }

        return new CapturedChatResponsePayload
        {
            ResponseId = response.ResponseId,
            ModelId = response.ModelId,
            Messages = messages,
        };
    }

    public ChatResponse ToChatResponse()
    {
        var messages = new List<ChatMessage>(Messages.Count);
        foreach (var captured in Messages)
        {
            var role = string.IsNullOrEmpty(captured.Role)
                ? ChatRole.Assistant
                : new ChatRole(captured.Role);
            messages.Add(new ChatMessage(role, captured.Text ?? string.Empty));
        }

        return new ChatResponse(messages)
        {
            ResponseId = ResponseId,
            ModelId = ModelId,
        };
    }

    internal sealed class CapturedMessage
    {
        public string? Role { get; set; }
        public string? Text { get; set; }
    }
}
