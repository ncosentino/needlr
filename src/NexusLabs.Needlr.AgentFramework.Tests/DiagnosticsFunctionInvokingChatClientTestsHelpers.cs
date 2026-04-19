using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Minimal chat client that returns a tool call on the first request,
/// then a plain response on the second (after receiving function results).
/// </summary>
internal sealed class TestChatClient : IChatClient
{
    private readonly string _toolCallName;
    private readonly object? _toolResult;
    private int _callCount;

    internal TestChatClient(string toolCallName, object? toolResult)
    {
        _toolCallName = toolCallName;
        _toolResult = toolResult;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _callCount);
        if (count == 1)
        {
            var content = new FunctionCallContent(
                $"call-{count}",
                _toolCallName,
                new Dictionary<string, object?> { ["input"] = "test" });

            return Task.FromResult(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, [content])]));
        }

        return Task.FromResult(new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, $"Result: {_toolResult}")]));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
