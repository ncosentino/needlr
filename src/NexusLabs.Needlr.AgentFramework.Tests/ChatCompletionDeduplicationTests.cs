using System.Diagnostics;

using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests verifying that the iterative agent loop never produces duplicate
/// <see cref="ChatCompletionDiagnostics"/> entries regardless of pipeline composition.
/// </summary>
public sealed class ChatCompletionDeduplicationTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // B1: THE exact reported bug — pre-wrapped client produces exactly 1 entry per call, not 2
    [Fact]
    public async Task RunAsync_WithExistingDiagnosticsMiddleware_ProducesExactlyOneChatCompletionPerCall()
    {
        var mockChat = CreateToolCallThenDoneChat("SearchTool", inputTokens: 100, outputTokens: 50);
        var tool = CreateTool("SearchTool", () => "search result");

        // Pre-wrap with diagnostics middleware (simulates UsingDiagnostics)
        var middleware = new DiagnosticsChatClientMiddleware();
        var wrappedClient = new DiagnosticsRecordingChatClient(mockChat.Object, middleware);

        var loop = CreateLoop(wrappedClient);

        var result = await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        // 2 actual LLM calls (tool call round + final text response)
        Assert.Equal(2, result.Diagnostics!.ChatCompletions.Count);
    }

    // B2: Token counts match unique entries (catches the 2× inflation)
    [Fact]
    public async Task RunAsync_WithExistingDiagnosticsMiddleware_TokenCountsMatchUniqueEntries()
    {
        var mockChat = CreateToolCallThenDoneChat("DataTool", inputTokens: 200, outputTokens: 100);
        var tool = CreateTool("DataTool", () => "data");

        var middleware = new DiagnosticsChatClientMiddleware();
        var wrappedClient = new DiagnosticsRecordingChatClient(mockChat.Object, middleware);

        var loop = CreateLoop(wrappedClient);

        var result = await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        var expectedTotal = result.Diagnostics!.ChatCompletions
            .Sum(c => c.Tokens.TotalTokens);
        Assert.Equal(expectedTotal, result.Diagnostics!.AggregateTokenUsage.TotalTokens);
    }

    // B3: Without existing middleware, loop installs its own and records correctly
    [Fact]
    public async Task RunAsync_WithoutExistingDiagnosticsMiddleware_StillRecordsChatCompletions()
    {
        var mockChat = CreateTextResponseChat("Hello");
        var loop = CreateLoop(mockChat.Object);

        var result = await loop.RunAsync(CreateOptions([]), CreateContext(), _ct);

        Assert.Single(result.Diagnostics!.ChatCompletions);
    }

    // B4: Per-loop factory wraps with diagnostics, loop detects and skips
    [Fact]
    public async Task RunAsync_WithPerLoopFactory_ThatWrapsWithDiagnostics_NoDuplication()
    {
        var mockChat = CreateToolCallThenDoneChat("PerLoopTool", inputTokens: 50, outputTokens: 25);
        var tool = CreateTool("PerLoopTool", () => "result");

        var loop = CreateLoop(mockChat.Object);

        var options = CreateOptions([tool]);
        options.ChatClientFactory = inner =>
        {
            // Per-loop factory wraps with diagnostics
            var middleware = new DiagnosticsChatClientMiddleware();
            return new DiagnosticsRecordingChatClient(inner, middleware);
        };

        var result = await loop.RunAsync(options, CreateContext(), _ct);

        Assert.Equal(2, result.Diagnostics!.ChatCompletions.Count);
    }

    // B5: Tool calls are never duplicated (recorded by loop, not middleware)
    [Fact]
    public async Task RunAsync_ToolCalls_NeverDuplicated()
    {
        var mockChat = CreateToolCallThenDoneChat("MyTool", inputTokens: 50, outputTokens: 25);
        var tool = CreateTool("MyTool", () => "tool output");

        var middleware = new DiagnosticsChatClientMiddleware();
        var wrappedClient = new DiagnosticsRecordingChatClient(mockChat.Object, middleware);

        var loop = CreateLoop(wrappedClient);

        var result = await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        // Exactly 1 tool call invocation
        Assert.Single(result.Diagnostics!.ToolCalls);
        Assert.Equal("MyTool", result.Diagnostics!.ToolCalls[0].ToolName);
    }

    // B6: All completions have unique sequence numbers
    [Fact]
    public async Task RunAsync_MultipleIterations_AllCompletionsHaveUniqueSequence()
    {
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n <= 4)
                {
                    return CreateToolCallResponse("IterTool", $"call-{n}", inputTokens: 100, outputTokens: 50);
                }

                return CreateTextResponse("done", inputTokens: 80, outputTokens: 40);
            });

        var tool = CreateTool("IterTool", () => "iteration result");

        var middleware = new DiagnosticsChatClientMiddleware();
        var wrappedClient = new DiagnosticsRecordingChatClient(mockChat.Object, middleware);

        var loop = CreateLoop(wrappedClient);

        var options = CreateOptions([tool]);
        options.MaxIterations = 10;

        var result = await loop.RunAsync(options, CreateContext(), _ct);

        var sequences = result.Diagnostics!.ChatCompletions.Select(c => c.Sequence).ToList();
        Assert.Equal(sequences.Count, sequences.Distinct().Count());
    }

    // B7: Loop never calls GetStreamingResponseAsync
    [Fact]
    public async Task RunAsync_NeverCallsGetStreamingResponseAsync()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]));

        // If streaming is called, fail hard
        mockChat
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new NotImplementedException(
                "GetStreamingResponseAsync should never be called by IterativeAgentLoop"));

        var loop = CreateLoop(mockChat.Object);

        var result = await loop.RunAsync(CreateOptions([]), CreateContext(), _ct);

        Assert.NotNull(result);
        Assert.True(result.Diagnostics!.Succeeded, "Loop should complete without calling streaming");
    }

    // E1
    [Fact]
    public void ChatCompletionCollectorHolder_HasRealCollector_DefaultIsFalse()
    {
        var holder = new ChatCompletionCollectorHolder();
        Assert.False(holder.HasRealCollector, "Expected default holder to have no real collector");
    }

    // E2
    [Fact]
    public void ChatCompletionCollectorHolder_HasRealCollector_AfterSetCollector_IsTrue()
    {
        var holder = new ChatCompletionCollectorHolder();
        var middleware = new DiagnosticsChatClientMiddleware();
        holder.SetCollector(middleware);
        Assert.True(holder.HasRealCollector, "Expected holder to report real collector after SetCollector");
    }

    // B8: Triple-wrap idempotency — wrapping 3 times still produces exactly 1 entry per call.
    // This proves the fix is categorically idempotent, not just "works for 2".
    [Fact]
    public async Task RunAsync_TripleWrapped_StillProducesExactlyOneChatCompletionPerCall()
    {
        var mockChat = CreateTextResponseChat("triple-done");

        // Wrap three times with DiagnosticsRecordingChatClient
        var mw1 = new DiagnosticsChatClientMiddleware();
        var wrap1 = new DiagnosticsRecordingChatClient(mockChat.Object, mw1);
        var mw2 = new DiagnosticsChatClientMiddleware();
        var wrap2 = new DiagnosticsRecordingChatClient(wrap1, mw2);
        var mw3 = new DiagnosticsChatClientMiddleware();
        var wrap3 = new DiagnosticsRecordingChatClient(wrap2, mw3);

        var loop = CreateLoop(wrap3);

        var result = await loop.RunAsync(CreateOptions([]), CreateContext(), _ct);

        // Must be exactly 1, not 2, 3, or 4
        Assert.Single(result.Diagnostics!.ChatCompletions);

        var expectedTotal = result.Diagnostics!.ChatCompletions
            .Sum(c => c.Tokens.TotalTokens);
        Assert.Equal(expectedTotal, result.Diagnostics!.AggregateTokenUsage.TotalTokens);
    }

    // B9: Per-loop factory that REPLACES the client (drops the inner chain entirely).
    // Diagnostics middleware from the factory is lost. The loop must install its own
    // so that diagnostics are still captured — not silently lost.
    [Fact]
    public async Task RunAsync_WithPerLoopFactory_ThatReplacesClient_StillRecordsDiagnostics()
    {
        // The accessor provides a client that has diagnostics middleware on it
        var originalMock = CreateTextResponseChat("original");
        var mw = new DiagnosticsChatClientMiddleware();
        var wrappedOriginal = new DiagnosticsRecordingChatClient(originalMock.Object, mw);

        var loop = CreateLoop(wrappedOriginal);

        // Per-loop factory completely REPLACES the client — does NOT wrap inner
        var replacementMock = CreateTextResponseChat("replaced");

        var options = CreateOptions([]);
        options.ChatClientFactory = _ => replacementMock.Object; // ignores inner!

        var result = await loop.RunAsync(options, CreateContext(), _ct);

        // The replacement client has no diagnostics middleware.
        // The loop must detect this and install its own.
        Assert.Single(result.Diagnostics!.ChatCompletions);
    }

    // B10: Replicates BrandGhost's exact pattern — UsingDiagnostics() wraps factory
    // client, then per-loop ChatClientFactory wraps via ChatClientBuilder (e.g.
    // UseChatReducer). GetService must still find DiagnosticsRecordingChatClient
    // through the builder output chain, preventing duplication.
    [Fact]
    public async Task RunAsync_WithChatClientBuilder_WrappingDiagnosticsClient_NoDuplication()
    {
        var mockChat = CreateToolCallThenDoneChat("BrandGhostTool", inputTokens: 150, outputTokens: 30);
        var tool = CreateTool("BrandGhostTool", () => "search result");

        // Simulate UsingDiagnostics: wrap mock with DiagnosticsRecordingChatClient
        var middleware = new DiagnosticsChatClientMiddleware();
        var diagnosticsClient = new DiagnosticsRecordingChatClient(mockChat.Object, middleware);

        var loop = CreateLoop(diagnosticsClient);

        var options = CreateOptions([tool]);
        // Simulate BrandGhost's BuildEffectiveChatClientFactory: uses ChatClientBuilder
        // to add middleware on top of the diagnostics-wrapped client
        options.ChatClientFactory = inner =>
        {
            return new ChatClientBuilder(inner)
                .Use(innerClient => new PassthroughClient(innerClient))
                .Build();
        };

        var result = await loop.RunAsync(options, CreateContext(), _ct);

        // Must be exactly 2 (tool call + text), not 4
        Assert.Equal(2, result.Diagnostics!.ChatCompletions.Count);

        var expectedTotal = result.Diagnostics!.ChatCompletions
            .Sum(c => c.Tokens.TotalTokens);
        Assert.Equal(expectedTotal, result.Diagnostics!.AggregateTokenUsage.TotalTokens);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IterativeAgentLoop CreateLoop(IChatClient chatClient, IAgentMetrics? metrics = null)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(chatClient);
        return new IterativeAgentLoop(accessor.Object, metrics: metrics);
    }

    private static IterativeContext CreateContext() =>
        new() { Workspace = new InMemoryWorkspace() };

    private static IterativeLoopOptions CreateOptions(IReadOnlyList<AITool> tools) =>
        new()
        {
            Instructions = "Test assistant",
            PromptFactory = _ => "Do the thing",
            Tools = tools,
            MaxIterations = 5,
            IsComplete = _ => true,
            LoopName = "dedup-test",
        };

    private static Mock<IChatClient> CreateToolCallThenDoneChat(
        string toolName, int inputTokens = 50, int outputTokens = 25)
    {
        var callCount = 0;
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 1)
                {
                    return CreateToolCallResponse(toolName, $"call-{n}", inputTokens, outputTokens);
                }

                return CreateTextResponse("done", inputTokens, outputTokens);
            });
        return mock;
    }

    private static Mock<IChatClient> CreateTextResponseChat(string text)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, text)])
            {
                ModelId = "test-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 10,
                    OutputTokenCount = 20,
                    TotalTokenCount = 30,
                },
            });
        return mock;
    }

    private static ChatResponse CreateToolCallResponse(
        string toolName, string callId, int inputTokens, int outputTokens)
    {
        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent(callId, toolName,
                    new Dictionary<string, object?> { ["arg"] = "val" })
            ])
        ])
        {
            ModelId = "test-model",
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
                TotalTokenCount = inputTokens + outputTokens,
            },
        };
        return response;
    }

    private static ChatResponse CreateTextResponse(string text, int inputTokens, int outputTokens)
    {
        return new ChatResponse([new ChatMessage(ChatRole.Assistant, text)])
        {
            ModelId = "test-model",
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
                TotalTokenCount = inputTokens + outputTokens,
            },
        };
    }

    private static AIFunction CreateTool(string name, Func<object?> execute)
    {
        return AIFunctionFactory.Create(
            () => execute(),
            new AIFunctionFactoryOptions { Name = name });
    }

    /// <summary>
    /// Simple DelegatingChatClient that passes through without modification.
    /// Simulates middleware like UseChatReducer being added on top of an existing chain.
    /// </summary>
    private sealed class PassthroughClient(IChatClient inner) : DelegatingChatClient(inner);
}
