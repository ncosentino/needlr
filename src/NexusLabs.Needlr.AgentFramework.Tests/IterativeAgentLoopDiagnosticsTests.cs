using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed partial class IterativeAgentLoopTests
{
    [Fact]
    public async Task RunAsync_Diagnostics_CapturesTokenCounts()
    {
        var mockChat = CreateMockChatWithTokens("done", inputTokens: 100, outputTokens: 25);
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        var iter = Assert.Single(result.Iterations);
        Assert.Equal(100, iter.Tokens.InputTokens);
        Assert.Equal(25, iter.Tokens.OutputTokens);
        Assert.Equal(1, iter.LlmCallCount);
    }

    [Fact]
    public async Task RunAsync_Diagnostics_CapturesToolCallCount()
    {
        var tool = CreateTool("ping", () => "pong");

        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return CreateToolCallResponse(
                        ("ping", "c1", null),
                        ("ping", "c2", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        Assert.Equal(2, result.Diagnostics.ToolCalls.Count);
        Assert.All(result.Diagnostics.ToolCalls, tc =>
        {
            Assert.Equal("ping", tc.ToolName);
            Assert.True(tc.Succeeded);
        });
    }

    [Fact]
    public async Task RunAsync_IterationRecords_InCorrectOrder()
    {
        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        var tool = CreateTool("noop", () => "ok");

        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 3)
                {
                    return CreateToolCallResponse(("noop", $"c{callCount}", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "final")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Iterations.Count);
        for (int i = 0; i < result.Iterations.Count; i++)
        {
            Assert.Equal(i, result.Iterations[i].Iteration);
        }

        // Last iteration should have the text response
        Assert.Equal("final", result.Iterations[3].FinalResponse?.Text);
    }

    [Fact]
    public async Task RunAsync_Iterations_HaveNonZeroDuration()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.True(iter.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_ChatCompletions_ContainsOneEntryPerLlmCall()
    {
        var mockChat = CreateMockChatWithTokens("done", 100, 50);
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Diagnostics);
        Assert.Single(result.Diagnostics!.ChatCompletions);
        Assert.True(result.Diagnostics.ChatCompletions[0].Succeeded);
    }

    [Fact]
    public async Task RunAsync_AggregateTokenUsage_MatchesSumOfIterationTokens()
    {
        var tool = CreateTool("ping", () => "pong");
        var callCount = 0;

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    var toolResponse = CreateToolCallResponse(("ping", $"c{callCount}", null));
                    toolResponse.Usage = new UsageDetails
                    {
                        InputTokenCount = 100 * callCount,
                        OutputTokenCount = 50 * callCount,
                        TotalTokenCount = 150 * callCount,
                    };
                    return toolResponse;
                }

                var textResponse = new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
                textResponse.Usage = new UsageDetails
                {
                    InputTokenCount = 300,
                    OutputTokenCount = 150,
                    TotalTokenCount = 450,
                };
                return textResponse;
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        var diag = result.Diagnostics!;

        // Sum tokens from IterationRecord
        long expectedInput = result.Iterations.Sum(i => i.Tokens.InputTokens);
        long expectedOutput = result.Iterations.Sum(i => i.Tokens.OutputTokens);
        long expectedTotal = result.Iterations.Sum(i => i.Tokens.TotalTokens);

        Assert.Equal(expectedInput, diag.AggregateTokenUsage.InputTokens);
        Assert.Equal(expectedOutput, diag.AggregateTokenUsage.OutputTokens);
        Assert.Equal(expectedTotal, diag.AggregateTokenUsage.TotalTokens);
        Assert.True(diag.AggregateTokenUsage.TotalTokens > 0);
    }

    [Fact]
    public async Task RunAsync_TotalInputAndOutputMessages_AreRecorded()
    {
        var mockChat = CreateMockChatWithTokens("done", 100, 50);
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        // [system, user] = 2 input messages
        Assert.True(result.Diagnostics!.TotalInputMessages >= 2);
        Assert.True(result.Diagnostics.TotalOutputMessages >= 1);
    }

    [Fact]
    public async Task RunAsync_OneRoundTrip_RecordsTwoChatCompletionEntries()
    {
        var tool = CreateTool("ping", () => "pong");
        var callCount = 0;

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var toolResponse = CreateToolCallResponse(("ping", "c1", null));
                    toolResponse.Usage = new UsageDetails
                    {
                        InputTokenCount = 100,
                        OutputTokenCount = 50,
                        TotalTokenCount = 150,
                    };
                    toolResponse.ModelId = "gpt-4o";
                    return toolResponse;
                }

                var textResponse = new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
                textResponse.Usage = new UsageDetails
                {
                    InputTokenCount = 200,
                    OutputTokenCount = 75,
                    TotalTokenCount = 275,
                };
                textResponse.ModelId = "gpt-4o";
                return textResponse;
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.OneRoundTrip);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        var completions = result.Diagnostics!.ChatCompletions;
        Assert.Equal(2, completions.Count);
        Assert.Equal(0, completions[0].Sequence);
        Assert.Equal(1, completions[1].Sequence);
        Assert.Equal("gpt-4o", completions[0].Model);
        Assert.Equal("gpt-4o", completions[1].Model);
        Assert.True(completions[0].Succeeded);
        Assert.True(completions[1].Succeeded);
    }

    [Fact]
    public async Task RunAsync_ChatCompletionFailure_IsRecordedInDiagnostics()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM provider error"));

        var loop = CreateLoop(mockChat);
        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Diagnostics);

        // The failed completion should still be recorded
        var completions = result.Diagnostics!.ChatCompletions;
        Assert.Single(completions);
        Assert.False(completions[0].Succeeded);
        Assert.Equal("LLM provider error", completions[0].ErrorMessage);
        Assert.Equal("unknown", completions[0].Model);
    }

    [Fact]
    public async Task RunAsync_InternalMiddleware_RecordsExactlyOneCompletionPerCall()
    {
        // The loop internally wraps the chat client with DiagnosticsChatClientMiddleware,
        // which is the single writer for chat completion diagnostics. Verify exactly one
        // entry per LLM call, with correct token counts.
        const int InputTokens = 100;
        const int OutputTokens = 50;

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var response = new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
                response.Usage = new UsageDetails
                {
                    InputTokenCount = InputTokens,
                    OutputTokenCount = OutputTokens,
                    TotalTokenCount = InputTokens + OutputTokens,
                };
                return response;
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions();

        var result = await loop.RunAsync(
            options,
            CreateContext(),
            TestContext.Current.CancellationToken);

        var completions = result.Diagnostics!.ChatCompletions;
        Assert.Single(completions);
        Assert.Equal(InputTokens, completions[0].Tokens.InputTokens);
        Assert.Equal(OutputTokens, completions[0].Tokens.OutputTokens);

        var aggregate = result.Diagnostics.AggregateTokenUsage;
        Assert.Equal(InputTokens, aggregate.InputTokens);
        Assert.Equal(OutputTokens, aggregate.OutputTokens);
        Assert.Equal(InputTokens + OutputTokens, aggregate.TotalTokens);
    }

    [Fact]
    public async Task RunAsync_InternalMiddleware_MultiRound_RecordsExactlyOneEntryPerCall()
    {
        // Multi-round variant: the loop's internal middleware records one chat
        // completion per LLM call across tool-call rounds. No external middleware.
        const int InputTokens = 200;
        const int OutputTokens = 75;

        var tool = CreateTool("ping", () => "pong");
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;

                if (callCount == 1)
                {
                    var toolResp = CreateToolCallResponse(("ping", "c1", null));
                    toolResp.Usage = new UsageDetails
                    {
                        InputTokenCount = InputTokens,
                        OutputTokenCount = OutputTokens,
                        TotalTokenCount = InputTokens + OutputTokens,
                    };
                    return toolResp;
                }

                var response = new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
                response.Usage = new UsageDetails
                {
                    InputTokenCount = InputTokens,
                    OutputTokenCount = OutputTokens,
                    TotalTokenCount = InputTokens + OutputTokens,
                };
                return response;
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.OneRoundTrip);

        var result = await loop.RunAsync(
            options,
            CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, callCount);

        var completions = result.Diagnostics!.ChatCompletions;
        Assert.Equal(2, completions.Count);

        var aggregate = result.Diagnostics.AggregateTokenUsage;
        Assert.Equal(2 * InputTokens, aggregate.InputTokens);
        Assert.Equal(2 * OutputTokens, aggregate.OutputTokens);
        Assert.Equal(2 * (InputTokens + OutputTokens), aggregate.TotalTokens);
    }

    [Fact]
    public async Task RunAsync_ChatCompletion_CapturesRequestAndResponsePayloads()
    {
        var mockChat = CreateMockChatWithTokens("done", 42, 17);
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            promptFactory: _ => "hello",
            isComplete: _ => true);
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        Assert.Single(result.Diagnostics.ChatCompletions);
        var chat = result.Diagnostics.ChatCompletions[0];
        Assert.NotNull(chat.RequestMessages);
        Assert.NotEmpty(chat.RequestMessages);
        Assert.True(chat.RequestCharCount > 0, "Expected RequestCharCount to reflect captured request messages");
        Assert.NotNull(chat.Response);
        Assert.True(chat.ResponseCharCount > 0, "Expected ResponseCharCount to reflect captured response");
    }

    [Fact]
    public async Task RunAsync_ToolCall_SingleCallMode_CapturesArgumentsAndResult()
    {
        await AssertToolCallPayloadCaptured(ToolResultMode.SingleCall);
    }

    [Fact]
    public async Task RunAsync_ToolCall_OneRoundTripMode_CapturesArgumentsAndResult()
    {
        await AssertToolCallPayloadCaptured(ToolResultMode.OneRoundTrip);
    }

    [Fact]
    public async Task RunAsync_ToolCall_MultiRoundMode_CapturesArgumentsAndResult()
    {
        await AssertToolCallPayloadCaptured(ToolResultMode.MultiRound);
    }

    private static async Task AssertToolCallPayloadCaptured(ToolResultMode toolResultMode)
    {
        var tool = CreateToolWithArgs("search", input => $"result-for-{input}");

        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return CreateToolCallResponse(
                        ("search", "call-1", new Dictionary<string, object?> { { "input", "query-value" } }));
                }
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            promptFactory: _ => "invoke tool",
            tools: [tool],
            maxIterations: 2,
            toolResultMode: toolResultMode);
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        Assert.NotEmpty(result.Diagnostics.ToolCalls);
        var toolCall = result.Diagnostics.ToolCalls[0];
        Assert.Equal("search", toolCall.ToolName);
        Assert.NotNull(toolCall.Arguments);
        Assert.True(toolCall.ArgumentsCharCount > 0, "Expected ArgumentsCharCount to reflect captured arguments");
        Assert.NotNull(toolCall.Result);
        Assert.True(toolCall.ResultCharCount > 0, "Expected ResultCharCount to reflect captured tool result");
    }

    [Fact]
    public async Task RunAsync_ChatCompletionSequences_AreUnique()
    {
        var tool = CreateTool("ping", () => "pong");
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    return CreateToolCallResponse(("ping", $"c{callCount}", null));
                }
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool], toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        Assert.Equal(3, result.Diagnostics.ChatCompletions.Count);

        var sequences = result.Diagnostics.ChatCompletions.Select(c => c.Sequence).ToList();
        Assert.Equal(sequences.Distinct().Count(), sequences.Count);
    }

    [Fact]
    public async Task RunAsync_AggregateTokens_EqualsSumOfChatCompletionTokens()
    {
        var tool = CreateTool("ping", () => "pong");
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var toolResp = CreateToolCallResponse(("ping", "c1", null));
                    toolResp.Usage = new UsageDetails
                    {
                        InputTokenCount = 100,
                        OutputTokenCount = 50,
                        TotalTokenCount = 150,
                    };
                    return toolResp;
                }

                var textResp = new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
                textResp.Usage = new UsageDetails
                {
                    InputTokenCount = 200,
                    OutputTokenCount = 100,
                    TotalTokenCount = 300,
                };
                return textResp;
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool], toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        var diag = result.Diagnostics;

        long expectedTotal = diag.ChatCompletions.Sum(c => c.Tokens.TotalTokens);
        long expectedInput = diag.ChatCompletions.Sum(c => c.Tokens.InputTokens);
        long expectedOutput = diag.ChatCompletions.Sum(c => c.Tokens.OutputTokens);

        Assert.Equal(expectedTotal, diag.AggregateTokenUsage.TotalTokens);
        Assert.Equal(expectedInput, diag.AggregateTokenUsage.InputTokens);
        Assert.Equal(expectedOutput, diag.AggregateTokenUsage.OutputTokens);
        Assert.Equal(450, diag.AggregateTokenUsage.TotalTokens);
    }

    [Fact]
    public async Task RunAsync_SuccessfulCompletions_AlwaysHavePayloads()
    {
        var tool = CreateToolWithArgs("search", input => $"found-{input}");
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return CreateToolCallResponse(
                        ("search", "call-1", new Dictionary<string, object?> { { "input", "test-query" } }));
                }
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "final answer")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool], toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        AssertDiagnosticsInvariants(result.Diagnostics);
    }

    [Fact]
    public async Task RunAsync_FailedChatCompletion_ProducesExactlyOneEntry()
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
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Provider is down");
                }
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(maxIterations: 2);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        var failed = result.Diagnostics.ChatCompletions.Where(c => !c.Succeeded).ToList();
        Assert.Single(failed);
        Assert.Equal("Provider is down", failed[0].ErrorMessage);

        Assert.NotNull(failed[0].RequestMessages);
        Assert.True(failed[0].RequestCharCount > 0,
            "Failed completion should still capture the request messages");

        var sequences = result.Diagnostics.ChatCompletions.Select(c => c.Sequence).ToList();
        Assert.Equal(sequences.Distinct().Count(), sequences.Count);
    }

    [Fact]
    public async Task RunAsync_MultiCallWithTools_SatisfiesAllInvariants()
    {
        var tool = CreateTool("ping", () => "pong");
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;

                ChatResponse resp;
                if (callCount == 1)
                {
                    resp = CreateToolCallResponse(("ping", "c1", null));
                }
                else
                {
                    resp = new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
                }

                resp.Usage = new UsageDetails
                {
                    InputTokenCount = 100,
                    OutputTokenCount = 50,
                    TotalTokenCount = 150,
                };

                return resp;
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool], toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);
        Assert.Equal(2, result.Diagnostics.ChatCompletions.Count);
        AssertDiagnosticsInvariants(result.Diagnostics);
    }

    [Fact]
    public async Task RunAsync_FailedToolCall_ProducesExactlyOneEntryWithArguments()
    {
        var tool = CreateToolWithArgs("boom", _ => throw new InvalidOperationException("tool exploded"));
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return CreateToolCallResponse(
                        ("boom", "call-1", new Dictionary<string, object?> { { "input", "kaboom" } }));
                }
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "recovered")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool], toolResultMode: ToolResultMode.SingleCall, maxIterations: 2);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Diagnostics);

        var failedTools = result.Diagnostics.ToolCalls.Where(t => !t.Succeeded).ToList();
        Assert.Single(failedTools);
        Assert.Equal("boom", failedTools[0].ToolName);
        Assert.Equal("tool exploded", failedTools[0].ErrorMessage);

        Assert.NotNull(failedTools[0].Arguments);
        Assert.True(failedTools[0].ArgumentsCharCount > 0,
            "Failed tool call should still capture the arguments");

        var sequences = result.Diagnostics.ToolCalls.Select(t => t.Sequence).ToList();
        Assert.Equal(sequences.Distinct().Count(), sequences.Count);
    }
}
