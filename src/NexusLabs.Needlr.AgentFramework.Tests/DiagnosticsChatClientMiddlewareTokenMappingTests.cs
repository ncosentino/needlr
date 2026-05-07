using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests that the non-streaming and streaming paths of
/// <see cref="DiagnosticsChatClientMiddleware"/> map cached and reasoning
/// token counts from MEAI's first-class <see cref="UsageDetails"/> properties
/// (CachedInputTokenCount, ReasoningTokenCount), with backward-compat fallback
/// to the legacy <see cref="UsageDetails.AdditionalCounts"/> dictionary keys
/// for custom <see cref="IChatClient"/> implementations.
/// </summary>
public sealed class DiagnosticsChatClientMiddlewareTokenMappingTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_PopulatesCachedAndReasoningTokens_FromUsageDetailsFirstClassProperties()
    {
        var middleware = new DiagnosticsChatClientMiddleware();
        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
            {
                ModelId = "azure-gpt-4.1",
                Usage = new UsageDetails
                {
                    InputTokenCount = 5000,
                    OutputTokenCount = 200,
                    TotalTokenCount = 5200,
                    CachedInputTokenCount = 3000,
                    ReasoningTokenCount = 50,
                },
            });

        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        var diag = builder.Build();
        var completion = Assert.Single(diag.ChatCompletions);
        Assert.Equal(5000, completion.Tokens.InputTokens);
        Assert.Equal(200, completion.Tokens.OutputTokens);
        Assert.Equal(5200, completion.Tokens.TotalTokens);
        Assert.Equal(3000, completion.Tokens.CachedInputTokens);
        Assert.Equal(50, completion.Tokens.ReasoningTokens);
    }

    [Fact]
    public async Task HandleAsync_FallsBackToAdditionalCounts_WhenFirstClassPropertiesAreNull()
    {
        var middleware = new DiagnosticsChatClientMiddleware();
        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
            {
                ModelId = "custom-provider",
                Usage = new UsageDetails
                {
                    InputTokenCount = 100,
                    OutputTokenCount = 50,
                    TotalTokenCount = 150,
                    AdditionalCounts = new AdditionalPropertiesDictionary<long>
                    {
                        ["CachedInputTokens"] = 42,
                        ["ReasoningTokens"] = 7,
                    },
                },
            });

        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        var diag = builder.Build();
        var completion = Assert.Single(diag.ChatCompletions);
        Assert.Equal(42, completion.Tokens.CachedInputTokens);
        Assert.Equal(7, completion.Tokens.ReasoningTokens);
    }

    [Fact]
    public async Task HandleAsync_FirstClassPropertyWins_OverAdditionalCountsValue()
    {
        var middleware = new DiagnosticsChatClientMiddleware();
        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
            {
                ModelId = "model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 1000,
                    OutputTokenCount = 100,
                    TotalTokenCount = 1100,
                    CachedInputTokenCount = 800,
                    ReasoningTokenCount = 25,
                    AdditionalCounts = new AdditionalPropertiesDictionary<long>
                    {
                        ["CachedInputTokens"] = 1,
                        ["ReasoningTokens"] = 1,
                    },
                },
            });

        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        var diag = builder.Build();
        var completion = Assert.Single(diag.ChatCompletions);
        Assert.Equal(800, completion.Tokens.CachedInputTokens);
        Assert.Equal(25, completion.Tokens.ReasoningTokens);
    }

    [Fact]
    public async Task HandleAsync_NoUsageDetails_RecordsZeroCachedAndReasoning()
    {
        var middleware = new DiagnosticsChatClientMiddleware();
        var mockInner = new Mock<IChatClient>();
        mockInner
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
            {
                ModelId = "model",
            });

        using var builder = AgentRunDiagnosticsBuilder.StartNew("Agent");
        await middleware.HandleAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            options: null,
            mockInner.Object,
            _ct);

        var diag = builder.Build();
        var completion = Assert.Single(diag.ChatCompletions);
        Assert.Equal(0, completion.Tokens.CachedInputTokens);
        Assert.Equal(0, completion.Tokens.ReasoningTokens);
    }
}
