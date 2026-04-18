using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class EvaluationCaptureChatClientTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_NullStore_ThrowsArgumentNullException()
    {
        using var inner = new ThrowingChatClient();

        Assert.Throws<ArgumentNullException>(
            () => new EvaluationCaptureChatClient(inner, store: null!));
    }

    [Fact]
    public async Task GetResponseAsync_NullMessages_ThrowsArgumentNullException()
    {
        using var inner = new ThrowingChatClient();
        var store = new InMemoryEvaluationCaptureStore();
        using var client = new EvaluationCaptureChatClient(inner, store);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.GetResponseAsync(messages: null!, cancellationToken: _ct));
    }

    [Fact]
    public async Task GetResponseAsync_CacheMiss_InvokesInnerAndPersists()
    {
        var expected = new ChatResponse(new ChatMessage(ChatRole.Assistant, "hello"))
        {
            ResponseId = "resp-1",
            ModelId = "model-a",
        };
        using var inner = new RecordingChatClient(expected);
        var store = new InMemoryEvaluationCaptureStore();
        using var client = new EvaluationCaptureChatClient(inner, store);

        var result = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            cancellationToken: _ct);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(1, store.SaveCount);
        Assert.Single(store.Entries);
        Assert.Equal("hello", result.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_CacheHit_ReplaysWithoutInvokingInner()
    {
        var cached = new ChatResponse(new ChatMessage(ChatRole.Assistant, "cached"))
        {
            ResponseId = "resp-cached",
            ModelId = "model-z",
        };
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var key = EvaluationCaptureChatClient.ComputeKey(messages, options: null);

        var store = new InMemoryEvaluationCaptureStore();
        await store.SaveAsync(key, cached, _ct);

        using var inner = new ThrowingChatClient();
        using var client = new EvaluationCaptureChatClient(inner, store);

        var result = await client.GetResponseAsync(messages, cancellationToken: _ct);

        Assert.Equal("cached", result.Messages[0].Text);
        Assert.Equal("resp-cached", result.ResponseId);
        Assert.Equal("model-z", result.ModelId);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_NullMessages_ThrowsArgumentNullException()
    {
        using var inner = new ThrowingChatClient();
        var store = new InMemoryEvaluationCaptureStore();
        using var client = new EvaluationCaptureChatClient(inner, store);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(
                messages: null!,
                cancellationToken: _ct))
            {
            }
        });
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CacheMiss_BuffersAndPersists()
    {
        var expected = new ChatResponse(new ChatMessage(ChatRole.Assistant, "streamed"))
        {
            ResponseId = "resp-s",
            ModelId = "model-s",
        };
        using var inner = new RecordingChatClient(expected);
        var store = new InMemoryEvaluationCaptureStore();
        using var client = new EvaluationCaptureChatClient(inner, store);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "go") },
            cancellationToken: _ct))
        {
            updates.Add(update);
        }

        Assert.Equal(1, inner.StreamingCallCount);
        Assert.Equal(1, store.SaveCount);
        Assert.Single(updates);
        Assert.Equal("streamed", updates[0].Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CacheHit_ReplaysWithoutInvokingInner()
    {
        var cached = new ChatResponse(new ChatMessage(ChatRole.Assistant, "hit"))
        {
            ResponseId = "resp-hit",
            ModelId = "model-hit",
        };
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };
        var key = EvaluationCaptureChatClient.ComputeKey(messages, options: null);

        var store = new InMemoryEvaluationCaptureStore();
        await store.SaveAsync(key, cached, _ct);

        using var inner = new ThrowingChatClient();
        using var client = new EvaluationCaptureChatClient(inner, store);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(messages, cancellationToken: _ct))
        {
            updates.Add(update);
        }

        Assert.Single(updates);
        Assert.Equal("hit", updates[0].Text);
        Assert.Equal("resp-hit", updates[0].ResponseId);
        Assert.Equal("model-hit", updates[0].ModelId);
    }

    [Fact]
    public void ComputeKey_SameInputs_ProducesStableHash()
    {
        var a = new[]
        {
            new ChatMessage(ChatRole.User, "one"),
            new ChatMessage(ChatRole.Assistant, "two"),
        };
        var b = new[]
        {
            new ChatMessage(ChatRole.User, "one"),
            new ChatMessage(ChatRole.Assistant, "two"),
        };

        var options = new ChatOptions { ModelId = "m", Temperature = 0.3f };

        var keyA = EvaluationCaptureChatClient.ComputeKey(a, options);
        var keyB = EvaluationCaptureChatClient.ComputeKey(b, options);

        Assert.Equal(keyA, keyB);
        Assert.Equal(64, keyA.Length);
    }

    [Fact]
    public void ComputeKey_DifferentText_ProducesDifferentHashes()
    {
        var a = new[] { new ChatMessage(ChatRole.User, "one") };
        var b = new[] { new ChatMessage(ChatRole.User, "two") };

        var keyA = EvaluationCaptureChatClient.ComputeKey(a, options: null);
        var keyB = EvaluationCaptureChatClient.ComputeKey(b, options: null);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void ComputeKey_DifferentModelId_ProducesDifferentHashes()
    {
        var messages = new[] { new ChatMessage(ChatRole.User, "hi") };

        var keyA = EvaluationCaptureChatClient.ComputeKey(messages, new ChatOptions { ModelId = "a" });
        var keyB = EvaluationCaptureChatClient.ComputeKey(messages, new ChatOptions { ModelId = "b" });

        Assert.NotEqual(keyA, keyB);
    }
}
