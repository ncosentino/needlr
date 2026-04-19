using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Tests;

internal sealed class FakeInnerChatClient : IChatClient
{
    private readonly bool _streaming;

    internal FakeInnerChatClient(bool streaming = false)
    {
        _streaming = streaming;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Response")])
        {
            ModelId = "test-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 10,
                OutputTokenCount = 20,
                TotalTokenCount = 30,
            },
        };
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent("Streamed")],
            ModelId = "test-model",
        };
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

internal sealed class PassthroughDelegatingChatClient : DelegatingChatClient
{
    internal PassthroughDelegatingChatClient(IChatClient inner) : base(inner) { }
}
