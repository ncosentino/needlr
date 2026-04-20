using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Serialization-friendly projection of a <see cref="ChatResponse"/> used by
/// <see cref="FileEvaluationCaptureStore"/>. Captures textual content, function
/// call payloads, and function result payloads in their original message order.
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
            var captured = new CapturedMessage
            {
                Role = message.Role.Value,
                Text = message.Text,
            };

            if (message.Contents.Count > 0)
            {
                var contents = new List<CapturedContent>();
                foreach (var content in message.Contents)
                {
                    switch (content)
                    {
                        case FunctionCallContent fc:
                            contents.Add(new CapturedContent
                            {
                                Kind = CapturedContentKind.FunctionCall,
                                CallId = fc.CallId,
                                Name = fc.Name,
                                Arguments = fc.Arguments is not null
                                    ? JsonSerializer.Serialize(fc.Arguments)
                                    : null,
                            });
                            break;
                        case FunctionResultContent fr:
                            contents.Add(new CapturedContent
                            {
                                Kind = CapturedContentKind.FunctionResult,
                                CallId = fr.CallId,
                                Result = fr.Result is not null
                                    ? JsonSerializer.Serialize(fr.Result)
                                    : null,
                            });
                            break;
                        case TextContent tc:
                            contents.Add(new CapturedContent
                            {
                                Kind = CapturedContentKind.Text,
                                Text = tc.Text,
                            });
                            break;
                    }
                }

                if (contents.Count > 0)
                {
                    captured.Contents = contents;
                }
            }

            messages.Add(captured);
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

            if (captured.Contents is { Count: > 0 })
            {
                var contentItems = new List<AIContent>(captured.Contents.Count);
                foreach (var c in captured.Contents)
                {
                    switch (c.Kind)
                    {
                        case CapturedContentKind.Text:
                            contentItems.Add(new TextContent(c.Text ?? string.Empty));
                            break;
                        case CapturedContentKind.FunctionCall:
                            IDictionary<string, object?>? args = null;
                            if (c.Arguments is not null)
                            {
                                args = JsonSerializer
                                    .Deserialize<Dictionary<string, object?>>(c.Arguments);
                            }
                            contentItems.Add(new FunctionCallContent(
                                c.CallId ?? string.Empty,
                                c.Name ?? string.Empty,
                                args));
                            break;
                        case CapturedContentKind.FunctionResult:
                            object? result = null;
                            if (c.Result is not null)
                            {
                                result = JsonSerializer.Deserialize<JsonElement>(c.Result);
                            }
                            contentItems.Add(new FunctionResultContent(
                                c.CallId ?? string.Empty,
                                result));
                            break;
                    }
                }
                messages.Add(new ChatMessage(role, contentItems));
            }
            else
            {
                messages.Add(new ChatMessage(role, captured.Text ?? string.Empty));
            }
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
        public List<CapturedContent>? Contents { get; set; }
    }

    internal sealed class CapturedContent
    {
        public CapturedContentKind Kind { get; set; }
        public string? Text { get; set; }
        public string? CallId { get; set; }
        public string? Name { get; set; }
        public string? Arguments { get; set; }
        public string? Result { get; set; }
    }

    internal enum CapturedContentKind
    {
        Text,
        FunctionCall,
        FunctionResult,
    }
}
