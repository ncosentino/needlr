using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

internal sealed class RecordingChatClient : IChatClient
{
    private readonly Func<IReadOnlyList<ChatMessage>, ChatResponse> _responder;

    public RecordingChatClient(Func<IReadOnlyList<ChatMessage>, ChatResponse> responder)
    {
        _responder = responder;
    }

    public RecordingChatClient(ChatResponse response)
        : this(_ => response)
    {
    }

    public int CallCount { get; private set; }
    public int StreamingCallCount { get; private set; }

    public void Dispose()
    {
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        return Task.FromResult(_responder(materialized));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamingCallCount++;
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var response = _responder(materialized);
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Text)
            {
                ResponseId = response.ResponseId,
                ModelId = response.ModelId,
            };
        }

        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
