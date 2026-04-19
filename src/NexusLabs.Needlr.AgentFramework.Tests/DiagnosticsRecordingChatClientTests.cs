using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="DiagnosticsRecordingChatClient"/> — the DelegatingChatClient
/// wrapper that makes diagnostics middleware detectable via <c>GetService</c>.
/// </summary>
public sealed class DiagnosticsRecordingChatClientTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void GetService_ReturnsSelf_WhenQueriedByType()
    {
        var inner = new FakeInnerChatClient();
        var middleware = new DiagnosticsChatClientMiddleware();
        using var client = new DiagnosticsRecordingChatClient(inner, middleware);

        var result = client.GetService<DiagnosticsRecordingChatClient>();

        Assert.NotNull(result);
        Assert.Same(client, result);
    }

    [Fact]
    public void GetService_ReturnsSelf_ThroughDelegationChain()
    {
        var inner = new FakeInnerChatClient();
        var middleware = new DiagnosticsChatClientMiddleware();
        using var recording = new DiagnosticsRecordingChatClient(inner, middleware);

        // Wrap the recording client in another delegating client
        using var outer = recording
            .AsBuilder()
            .Use(innerClient => new PassthroughDelegatingChatClient(innerClient))
            .Build();

        // GetService should walk the chain and find the DiagnosticsRecordingChatClient
        var result = outer.GetService<DiagnosticsRecordingChatClient>();

        Assert.NotNull(result);
        Assert.Same(recording, result);
    }

    [Fact]
    public async Task GetResponseAsync_DelegatesToMiddleware()
    {
        var inner = new FakeInnerChatClient();
        var middleware = new DiagnosticsChatClientMiddleware();
        using var client = new DiagnosticsRecordingChatClient(inner, middleware);

        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello")],
            options: null,
            cancellationToken: _ct);

        Assert.NotNull(response);

        // The middleware should have recorded a chat completion on the builder
        var diag = builder.Build();
        Assert.Single(diag.ChatCompletions);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_DelegatesToMiddleware()
    {
        var inner = new FakeInnerChatClient(streaming: true);
        var middleware = new DiagnosticsChatClientMiddleware();
        using var client = new DiagnosticsRecordingChatClient(inner, middleware);

        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello")],
            options: null,
            cancellationToken: _ct))
        {
            updates.Add(update);
        }

        Assert.NotEmpty(updates);

        // The middleware should have recorded a chat completion on the builder
        var diag = builder.Build();
        Assert.Single(diag.ChatCompletions);
    }

}
