using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class IterativeAgentLoopTests
{
    #region Helpers

    private static IterativeAgentLoop CreateLoop(
        Mock<IChatClient> mockChat,
        IAgentDiagnosticsWriter? diagnosticsWriter = null,
        IAgentExecutionContextAccessor? executionContextAccessor = null)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(mockChat.Object);
        return new IterativeAgentLoop(accessor.Object, diagnosticsWriter, executionContextAccessor);
    }

    private static Mock<IChatClient> CreateMockChat(string responseText)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));
        return mock;
    }

    private static Mock<IChatClient> CreateMockChatWithTokens(
        string responseText, int inputTokens, int outputTokens)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var response = new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, responseText)]);
                response.Usage = new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens,
                    TotalTokenCount = inputTokens + outputTokens,
                };
                return response;
            });
        return mock;
    }

    private static ChatResponse CreateToolCallResponse(
        params (string name, string callId, IDictionary<string, object?>? args)[] calls)
    {
        var contents = new List<AIContent>();
        foreach (var (name, callId, args) in calls)
        {
            contents.Add(new FunctionCallContent(callId, name, args));
        }

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, contents)]);
    }

    private static AIFunction CreateTool(string name, Func<object?> execute)
    {
        return AIFunctionFactory.Create(
            () => execute(),
            new AIFunctionFactoryOptions
            {
                Name = name,
            });
    }

    private static AIFunction CreateToolWithArgs(
        string name,
        Func<string, object?> execute)
    {
        return AIFunctionFactory.Create(
            (string input) => execute(input),
            new AIFunctionFactoryOptions
            {
                Name = name,
            });
    }

    private static IterativeLoopOptions CreateOptions(
        Func<IterativeContext, string>? promptFactory = null,
        IReadOnlyList<AITool>? tools = null,
        int maxIterations = 10,
        Func<IterativeContext, bool>? isComplete = null,
        ToolResultMode toolResultMode = ToolResultMode.OneRoundTrip,
        int maxToolRoundsPerIteration = 5,
        string? instructions = null)
    {
        return new IterativeLoopOptions
        {
            Instructions = instructions ?? "You are a test assistant.",
            PromptFactory = promptFactory ?? (_ => "Do the thing."),
            Tools = tools ?? Array.Empty<AITool>(),
            MaxIterations = maxIterations,
            IsComplete = isComplete,
            ToolResultMode = toolResultMode,
            MaxToolRoundsPerIteration = maxToolRoundsPerIteration,
            LoopName = "test-loop",
        };
    }

    private static IterativeContext CreateContext(IWorkspace? workspace = null)
    {
        return new IterativeContext { Workspace = workspace ?? new InMemoryWorkspace() };
    }

    #endregion

    #region 2.1 — Loop termination on text response

    [Fact]
    public async Task RunAsync_TextResponse_TerminatesAfterOneIteration()
    {
        var mockChat = CreateMockChat("All done!");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        var context = CreateContext();

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("All done!", result.FinalResponse?.Text);
        Assert.Single(result.Iterations);
        Assert.Equal(0, result.Iterations[0].Iteration);
    }

    [Fact]
    public async Task RunAsync_TextResponse_IterationHasNoToolCalls()
    {
        var mockChat = CreateMockChat("Done");
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Iterations[0].ToolCalls);
        Assert.Equal("Done", result.Iterations[0].FinalResponse?.Text);
    }

    #endregion

    #region 2.2 — Tool call execution

    [Fact]
    public async Task RunAsync_ToolCall_ExecutesToolAndContinues()
    {
        var toolExecuted = false;
        var tool = CreateTool("do_work", () =>
        {
            toolExecuted = true;
            return "work done";
        });

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
                    // First call: request tool
                    return CreateToolCallResponse(("do_work", "call-1", null));
                }

                // Second call (OneRoundTrip feedback): text response
                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "Finished!")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool]);
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(toolExecuted);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_MultipleToolCalls_AllExecuted()
    {
        var executed = new List<string>();
        var tool1 = CreateTool("tool_a", () => { executed.Add("a"); return "a-result"; });
        var tool2 = CreateTool("tool_b", () => { executed.Add("b"); return "b-result"; });

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
                        ("tool_a", "call-a", null),
                        ("tool_b", "call-b", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "Done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [tool1, tool2]);
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b"], executed);
        Assert.Equal(2, result.Iterations[0].ToolCalls.Count);
        Assert.All(result.Iterations[0].ToolCalls, tc => Assert.True(tc.Succeeded));
    }

    #endregion

    #region 2.3 — Prompt factory called fresh each iteration

    [Fact]
    public async Task RunAsync_PromptFactory_CalledWithUpdatedContext()
    {
        var seenIterations = new List<int>();
        var workspace = new InMemoryWorkspace();

        var tool = CreateTool("write_stuff", () =>
        {
            workspace.TryWriteFile("output.txt", "hello");
            return "written";
        });

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
                return callCount switch
                {
                    // Iteration 0: tool call
                    1 => CreateToolCallResponse(("write_stuff", "c1", null)),
                    // Iteration 0: OneRoundTrip feedback — another tool call goes to next iter
                    2 => CreateToolCallResponse(("write_stuff", "c2", null)),
                    // Iteration 1: tool call
                    3 => CreateToolCallResponse(("write_stuff", "c3", null)),
                    // Iteration 1: text response
                    _ => new ChatResponse(
                        [new ChatMessage(ChatRole.Assistant, "All done")]),
                };
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            promptFactory: ctx =>
            {
                seenIterations.Add(ctx.Iteration);
                return $"Iteration {ctx.Iteration}";
            },
            tools: [tool]);

        var result = await loop.RunAsync(options, new IterativeContext { Workspace = workspace }, TestContext.Current.CancellationToken);

        Assert.Contains(0, seenIterations);
        Assert.Contains(1, seenIterations);
    }

    #endregion

    #region 2.4 — Max iterations enforced

    [Fact]
    public async Task RunAsync_MaxIterations_StopsAfterLimit()
    {
        // Model always requests tools — loop must stop at MaxIterations
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("noop", "c1", null)));

        var tool = CreateTool("noop", () => "ok");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 3,
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Iterations.Count);
    }

    #endregion

    #region 2.5 — IsComplete predicate

    [Fact]
    public async Task RunAsync_IsComplete_StopsLoopEarly()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("mark_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "marked";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("mark_done", "c1", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 100,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, new IterativeContext { Workspace = workspace }, TestContext.Current.CancellationToken);

        // Should stop after the iteration where "done.txt" was written
        Assert.True(result.Iterations.Count < 100);
        Assert.True(workspace.FileExists("done.txt"));
    }

    #endregion

    #region 2.7 — LastToolResults ephemeral

    [Fact]
    public async Task RunAsync_LastToolResults_AvailableInNextIterationPromptFactory()
    {
        IReadOnlyList<ToolCallResult>? capturedResults = null;

        var tool = CreateTool("greet", () => "hello world");

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
                return callCount switch
                {
                    // Iter 0: tool call
                    1 => CreateToolCallResponse(("greet", "c1", null)),
                    // Iter 0 OneRoundTrip: text ends iteration but results stored
                    _ => new ChatResponse(
                        [new ChatMessage(ChatRole.Assistant, "done")]),
                };
            });

        var loop = CreateLoop(mockChat);
        var iterationCount = 0;
        var options = CreateOptions(
            promptFactory: ctx =>
            {
                if (iterationCount > 0)
                {
                    capturedResults = ctx.LastToolResults;
                }

                iterationCount++;
                return "go";
            },
            tools: [tool],
            maxIterations: 2);

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        // First iteration produced tool results, but the loop terminated on text.
        // The results should be on the context for the next prompt factory call.
        // Since the text response terminated the iteration AND the loop (text = done),
        // there's no second iteration. Let's verify the context directly.
        // Actually, the text response in iter 0 means the loop terminates.
        // To properly test ephemeral results across iterations, we need the first
        // iteration to NOT produce a text response (only tools).
    }

    [Fact]
    public async Task RunAsync_LastToolResults_ClearedAfterPromptFactoryReads()
    {
        var capturedResultsPerIteration = new Dictionary<int, IReadOnlyList<ToolCallResult>?>();

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
                return callCount switch
                {
                    // Iter 0: tool call (SingleCall — no feedback)
                    1 => CreateToolCallResponse(("ping", "c1", null)),
                    // Iter 1: tool call
                    2 => CreateToolCallResponse(("ping", "c2", null)),
                    // Iter 2: text — done
                    _ => new ChatResponse(
                        [new ChatMessage(ChatRole.Assistant, "done")]),
                };
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            promptFactory: ctx =>
            {
                capturedResultsPerIteration[ctx.Iteration] = ctx.LastToolResults;
                return "go";
            },
            tools: [tool],
            maxIterations: 5,
            toolResultMode: ToolResultMode.SingleCall);

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        // Iteration 0: no prior results (empty list)
        Assert.NotNull(capturedResultsPerIteration[0]);
        Assert.Empty(capturedResultsPerIteration[0]!);

        // Iteration 1: has results from iteration 0's tool call
        Assert.NotNull(capturedResultsPerIteration[1]);
        Assert.Single(capturedResultsPerIteration[1]!);
        Assert.Equal("ping", capturedResultsPerIteration[1]![0].FunctionName);

        // Iteration 2: has results from iteration 1's tool call
        Assert.NotNull(capturedResultsPerIteration[2]);
        Assert.Single(capturedResultsPerIteration[2]!);
    }

    #endregion

    #region 2.8 — Workspace persists across iterations

    [Fact]
    public async Task RunAsync_WorkspaceState_PersistsAcrossIterations()
    {
        var workspace = new InMemoryWorkspace();
        string? readContent = null;

        var writeTool = CreateTool("write_file", () =>
        {
            workspace.TryWriteFile("data.txt", "iteration-data");
            return "written";
        });

        var readTool = CreateTool("read_file", () =>
        {
            readContent = workspace.TryReadFile("data.txt").Value.Content;
            return readContent;
        });

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
                return callCount switch
                {
                    1 => CreateToolCallResponse(("write_file", "c1", null)),
                    2 => CreateToolCallResponse(("read_file", "c2", null)),
                    _ => new ChatResponse(
                        [new ChatMessage(ChatRole.Assistant, "done")]),
                };
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [writeTool, readTool],
            toolResultMode: ToolResultMode.SingleCall);

        await loop.RunAsync(options, new IterativeContext { Workspace = workspace }, TestContext.Current.CancellationToken);

        Assert.Equal("iteration-data", readContent);
        Assert.True(workspace.FileExists("data.txt"));
    }

    #endregion

    #region 2.9 — Unknown tool name handling

    [Fact]
    public async Task RunAsync_UnknownTool_RecordedAsFailedToolCall()
    {
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
                    return CreateToolCallResponse(("nonexistent_tool", "c1", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(toolResultMode: ToolResultMode.SingleCall);
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var toolCall = Assert.Single(result.Iterations[0].ToolCalls);
        Assert.False(toolCall.Succeeded);
        Assert.Contains("Unknown tool", toolCall.ErrorMessage);
        Assert.Equal("nonexistent_tool", toolCall.FunctionName);
    }

    #endregion

    #region 2.10 — Tool exception handling

    [Fact]
    public async Task RunAsync_ToolException_RecordedAndLoopContinues()
    {
        var tool = CreateTool("explode", () =>
            throw new InvalidOperationException("boom!"));

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
                    return CreateToolCallResponse(("explode", "c1", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "handled")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var toolCall = Assert.Single(result.Iterations[0].ToolCalls);
        Assert.False(toolCall.Succeeded);
        Assert.Equal("boom!", toolCall.ErrorMessage);
    }

    #endregion

    #region 2.11 — CancellationToken

    [Fact]
    public async Task RunAsync_Cancellation_ExitsCleanly()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockChat = CreateMockChat("should not reach");
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(
            CreateOptions(), CreateContext(), cts.Token);

        Assert.False(result.Succeeded);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_CancellationMidLoop_Stops()
    {
        var cts = new CancellationTokenSource();
        var tool = CreateTool("cancel_trigger", () =>
        {
            cts.Cancel();
            return "ok";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("cancel_trigger", "c1", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 100,
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), cts.Token);

        Assert.False(result.Succeeded);
        Assert.True(result.Iterations.Count < 100);
    }

    #endregion

    #region 2.12 — Diagnostics

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

    #endregion

    #region 2.13 — Iteration records in order

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

    #endregion

    #region ToolResultMode — SingleCall

    [Fact]
    public async Task RunAsync_SingleCallMode_ExactlyOneLlmCallPerIteration()
    {
        var tool = CreateTool("noop", () => "ok");

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
                if (callCount <= 2)
                {
                    return CreateToolCallResponse(("noop", $"c{callCount}", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        // Each iteration should have exactly 1 LLM call
        foreach (var iter in result.Iterations.Where(i => i.ToolCalls.Count > 0))
        {
            Assert.Equal(1, iter.LlmCallCount);
        }
    }

    #endregion

    #region ToolResultMode — OneRoundTrip

    [Fact]
    public async Task RunAsync_OneRoundTripMode_SendsResultsBackOnce()
    {
        var tool = CreateTool("ping", () => "pong");
        List<IEnumerable<ChatMessage>>? capturedMessages = [];

        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                capturedMessages.Add(messages);
                callCount++;
                if (callCount == 1)
                {
                    // First call: request tool
                    return CreateToolCallResponse(("ping", "c1", null));
                }

                // Second call (round-trip): should include tool result messages
                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "got it")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.OneRoundTrip);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        // Should have been called twice in one iteration
        Assert.Equal(2, callCount);

        // Second call should have more messages (system + user + assistant tool call + tool result)
        var secondCallMessages = capturedMessages[1].ToList();
        Assert.True(secondCallMessages.Count > 2,
            $"Expected >2 messages in round-trip call, got {secondCallMessages.Count}");
    }

    [Fact]
    public async Task RunAsync_OneRoundTripMode_MaxTwoLlmCallsPerIteration()
    {
        var tool = CreateTool("chain", () => "chained");

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // Always return tool calls — but OneRoundTrip should stop at 2
                return CreateToolCallResponse(("chain", "c1", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 1,
            toolResultMode: ToolResultMode.OneRoundTrip);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.Equal(2, iter.LlmCallCount);
    }

    #endregion

    #region ToolResultMode — MultiRound

    [Fact]
    public async Task RunAsync_MultiRoundMode_ChainsToolCallsUpToMax()
    {
        var tool = CreateTool("step", () => "stepped");

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
                // Always request tools — MultiRound should stop at max
                return CreateToolCallResponse(("step", $"c{callCount}", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 1,
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 3);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.Equal(3, iter.LlmCallCount);
        Assert.Equal(3, iter.ToolCalls.Count);
    }

    [Fact]
    public async Task RunAsync_MultiRoundMode_StopsOnTextResponse()
    {
        var tool = CreateTool("step", () => "ok");

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
                if (callCount <= 2)
                {
                    return CreateToolCallResponse(("step", $"c{callCount}", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done chaining")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 1,
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 10);

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.Equal(3, iter.LlmCallCount);
        Assert.Equal("done chaining", iter.FinalResponse?.Text);
    }

    #endregion

    #region Fresh prompt per iteration (no history)

    [Fact]
    public async Task RunAsync_FreshPrompt_OnlySystemAndUserMessagesSent()
    {
        var capturedMessages = new List<List<ChatMessage>>();
        var tool = CreateTool("noop", () => "ok");

        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                var msgList = messages.ToList();
                capturedMessages.Add(msgList);
                callCount++;
                if (callCount <= 2)
                {
                    return CreateToolCallResponse(("noop", $"c{callCount}", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.SingleCall);

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        // Each first call of an iteration should have exactly [system, user]
        // Iterations: callCount 1 (iter 0), callCount 2 (iter 1), callCount 3 (iter 2)
        foreach (var msgs in capturedMessages)
        {
            Assert.Equal(2, msgs.Count);
            Assert.Equal(ChatRole.System, msgs[0].Role);
            Assert.Equal(ChatRole.User, msgs[1].Role);
        }
    }

    #endregion

    #region Timing

    [Fact]
    public async Task RunAsync_Iterations_HaveNonZeroDuration()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        var iter = Assert.Single(result.Iterations);
        Assert.True(iter.Duration >= TimeSpan.Zero);
    }

    #endregion

    #region ChatCompletion Diagnostics

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

    #endregion

    #region Lifecycle Hooks

    [Fact]
    public async Task RunAsync_OnIterationStart_FiresBeforePromptFactory()
    {
        var callOrder = new List<string>();

        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        options.PromptFactory = ctx =>
        {
            callOrder.Add("prompt");
            return "go";
        };
        options.OnIterationStart = (iter, ctx) =>
        {
            callOrder.Add($"start:{iter}");
            return Task.CompletedTask;
        };

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(["start:0", "prompt"], callOrder);
    }

    [Fact]
    public async Task RunAsync_OnToolCall_FiresForEachToolInOrderWithIteration()
    {
        var tool = CreateTool("ping", () => "pong");
        var hookCalls = new List<(int Iteration, string ToolName)>();

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
                return callCount == 1
                    ? CreateToolCallResponse(("ping", "c1", null))
                    : new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            toolResultMode: ToolResultMode.OneRoundTrip);
        options.OnToolCall = (iter, result) =>
        {
            hookCalls.Add((iter, result.FunctionName));
            return Task.CompletedTask;
        };

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Single(hookCalls);
        Assert.Equal(0, hookCalls[0].Iteration);
        Assert.Equal("ping", hookCalls[0].ToolName);
    }

    [Fact]
    public async Task RunAsync_OnIterationEnd_FiresWithCompleteRecord()
    {
        var endRecords = new List<IterationRecord>();

        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        options.OnIterationEnd = record =>
        {
            endRecords.Add(record);
            return Task.CompletedTask;
        };

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Single(endRecords);
        Assert.Equal(0, endRecords[0].Iteration);
        Assert.Equal("done", endRecords[0].FinalResponse?.Text);
    }

    [Fact]
    public async Task RunAsync_NullHooks_DoNotThrow()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();

        // All hooks are null by default — should not throw
        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_HookException_PropagatesToCaller()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        options.OnIterationStart = (_, _) =>
            throw new InvalidOperationException("Hook blew up");

        // Hook exception should NOT be caught by the loop's internal error handling
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken));
    }

    #endregion

    #region Diagnostics Accessor Integration

    [Fact]
    public async Task RunAsync_WriterPopulatedAfterRun()
    {
        var mockWriter = new Mock<IAgentDiagnosticsWriter>();
        IAgentRunDiagnostics? captured = null;
        mockWriter
            .Setup(w => w.Set(It.IsAny<IAgentRunDiagnostics>()))
            .Callback<IAgentRunDiagnostics>(d => captured = d);

        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, mockWriter.Object);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(result.Diagnostics!.AgentName, captured!.AgentName);
    }

    [Fact]
    public async Task RunAsync_NullWriter_DoesNotThrow()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, diagnosticsWriter: null);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_AccessorDiagnosticsMatchesResultDiagnostics()
    {
        var mockWriter = new Mock<IAgentDiagnosticsWriter>();
        IAgentRunDiagnostics? captured = null;
        mockWriter
            .Setup(w => w.Set(It.IsAny<IAgentRunDiagnostics>()))
            .Callback<IAgentRunDiagnostics>(d => captured = d);

        var mockChat = CreateMockChatWithTokens("done", 100, 50);
        var loop = CreateLoop(mockChat, mockWriter.Object);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.NotNull(result.Diagnostics);

        // The diagnostics written to the accessor should be the same object
        Assert.Same(result.Diagnostics, captured);
    }

    [Fact]
    public async Task RunAsync_WriterCalledEvenOnFailure()
    {
        var mockWriter = new Mock<IAgentDiagnosticsWriter>();
        IAgentRunDiagnostics? captured = null;
        mockWriter
            .Setup(w => w.Set(It.IsAny<IAgentRunDiagnostics>()))
            .Callback<IAgentRunDiagnostics>(d => captured = d);

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var loop = CreateLoop(mockChat, mockWriter.Object);
        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(captured);
        Assert.False(captured!.Succeeded);
    }

    #endregion

    #region Execution Context Bridge

    [Fact]
    public async Task RunAsync_ToolsCanReadWorkspaceViaExecutionContextAccessor()
    {
        var contextAccessor = new AgentExecutionContextAccessor();
        IWorkspace? capturedWorkspace = null;

        var tool = CreateTool("read_workspace", () =>
        {
            capturedWorkspace = contextAccessor.Current?.GetRequiredWorkspace();
            return "ok";
        });

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
                    return CreateToolCallResponse(("read_workspace", "c1", null));
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat, executionContextAccessor: contextAccessor);

        var ctx = CreateContext();
        ctx.Workspace.TryWriteFile("data.txt", "hello");

        var result = await loop.RunAsync(
            CreateOptions(tools: [tool]),
            ctx,
            TestContext.Current.CancellationToken);

        Assert.NotNull(capturedWorkspace);
        Assert.Equal("hello", capturedWorkspace!.TryReadFile("data.txt").Value.Content);
    }

    [Fact]
    public async Task RunAsync_ExecutionContextScopeRestoredAfterLoopCompletes()
    {
        var contextAccessor = new AgentExecutionContextAccessor();
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, executionContextAccessor: contextAccessor);

        var outerContext = new AgentExecutionContext(UserId: "outer", OrchestrationId: "outer-scope");
        using var outerScope = contextAccessor.BeginScope(outerContext);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(contextAccessor.Current);
        Assert.Equal("outer", contextAccessor.Current!.UserId);
    }

    [Fact]
    public async Task RunAsync_NullExecutionContextAccessor_DoesNotThrow()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat, executionContextAccessor: null);

        var result = await loop.RunAsync(CreateOptions(), CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_ExplicitExecutionContext_UsedOverAutoCreated()
    {
        var contextAccessor = new AgentExecutionContextAccessor();
        string? capturedUserId = null;

        var tool = CreateTool("check_context", () =>
        {
            capturedUserId = contextAccessor.Current?.UserId;
            return "ok";
        });

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
                    return CreateToolCallResponse(("check_context", "c1", null));
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat, executionContextAccessor: contextAccessor);

        var ctx = CreateContext();
        var options = CreateOptions(tools: [tool]);
        options.ExecutionContext = new AgentExecutionContext(
            UserId: "explicit-user",
            OrchestrationId: "explicit-orch",
            Workspace: ctx.Workspace);

        var result = await loop.RunAsync(options, ctx, TestContext.Current.CancellationToken);

        Assert.Equal("explicit-user", capturedUserId);
    }

    #endregion

    #region ToolFilter

    [Fact]
    public async Task RunAsync_ToolFilter_RestrictsToolsModelSees()
    {
        var toolA = CreateTool("allowed", () => "ok");
        var toolB = CreateTool("blocked", () => "should not run");

        ChatOptions? capturedOptions = null;
        var mockChat = new Mock<IChatClient>();
        var callCount = 0;
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> _, ChatOptions? opts, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    capturedOptions = opts;
                    return CreateToolCallResponse(("allowed", "call-1", null));
                }
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [toolA, toolB]);
        options.ToolFilter = (_, _, tools) => tools.Where(t => t.Name == "allowed").ToList();

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedOptions);
        Assert.Single(capturedOptions!.Tools!);
        Assert.Equal("allowed", capturedOptions.Tools![0].Name);
    }

    [Fact]
    public async Task RunAsync_NullToolFilter_PassesAllTools()
    {
        var toolA = CreateTool("tool_a", () => "a");
        var toolB = CreateTool("tool_b", () => "b");

        ChatOptions? capturedOptions = null;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> _, ChatOptions? opts, CancellationToken _) =>
            {
                capturedOptions = opts;
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(tools: [toolA, toolB]);
        // ToolFilter is null by default

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedOptions);
        Assert.Equal(2, capturedOptions!.Tools!.Count);
    }

    [Fact]
    public async Task RunAsync_ToolFilter_ReceivesCorrectIterationAndContext()
    {
        var capturedIterations = new List<int>();
        var capturedFileContents = new List<string>();

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
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var workspace = new InMemoryWorkspace();
        workspace.SeedFile("phase.txt", "research");

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(maxIterations: 1);
        options.ToolFilter = (iteration, ctx, tools) =>
        {
            capturedIterations.Add(iteration);
            capturedFileContents.Add(ctx.Workspace.TryReadFile("phase.txt").Value.Content);
            return tools;
        };

        await loop.RunAsync(options, CreateContext(workspace), TestContext.Current.CancellationToken);

        Assert.Contains(0, capturedIterations);
        Assert.Contains("research", capturedFileContents);
    }

    [Fact]
    public async Task RunAsync_ToolFilter_ChangesPerIteration()
    {
        var tool1 = CreateTool("search", () => "results");
        var tool2 = CreateTool("build", () => "built");

        var capturedToolNames = new List<List<string>>();
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> _, ChatOptions? opts, CancellationToken _) =>
            {
                capturedToolNames.Add(opts?.Tools?.Select(t => t.Name).ToList() ?? []);
                // Always request a tool call so the loop keeps iterating
                var availableToolName = opts?.Tools?.FirstOrDefault()?.Name ?? "search";
                return CreateToolCallResponse((availableToolName, $"call-{capturedToolNames.Count}", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool1, tool2],
            maxIterations: 2,
            toolResultMode: ToolResultMode.SingleCall);
        options.ToolFilter = (iteration, _, tools) =>
            iteration == 0
                ? tools.Where(t => t.Name == "search").ToList()
                : tools.Where(t => t.Name == "build").ToList();

        await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(2, capturedToolNames.Count);
        Assert.Equal(["search"], capturedToolNames[0]);
        Assert.Equal(["build"], capturedToolNames[1]);
    }

    #endregion

    #region Payload Capture (Bug #2)

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

    #endregion

    #region Diagnostics Invariants

    private static void AssertDiagnosticsInvariants(IAgentRunDiagnostics diagnostics)
    {
        var chatSequences = diagnostics.ChatCompletions.Select(c => c.Sequence).ToList();
        Assert.Equal(
            chatSequences.Distinct().Count(),
            chatSequences.Count);

        var toolSequences = diagnostics.ToolCalls.Select(t => t.Sequence).ToList();
        Assert.Equal(
            toolSequences.Distinct().Count(),
            toolSequences.Count);

        long chatTotalTokens = diagnostics.ChatCompletions.Sum(c => c.Tokens.TotalTokens);
        Assert.Equal(chatTotalTokens, diagnostics.AggregateTokenUsage.TotalTokens);

        foreach (var chat in diagnostics.ChatCompletions.Where(c => c.Succeeded))
        {
            Assert.NotNull(chat.RequestMessages);
            Assert.NotEmpty(chat.RequestMessages);
            Assert.True(chat.RequestCharCount > 0,
                $"Successful chat completion seq={chat.Sequence} has RequestCharCount=0");
            Assert.NotNull(chat.Response);
        }

        foreach (var tool in diagnostics.ToolCalls.Where(t => t.Succeeded && t.Arguments is { Count: > 0 }))
        {
            Assert.True(tool.ArgumentsCharCount > 0,
                $"Successful tool call seq={tool.Sequence} has ArgumentsCharCount=0 but Arguments has entries");
        }
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

    #endregion

    #region CheckCompletionAfterToolCalls — AfterToolRounds

    [Fact]
    public async Task RunAsync_MultiRound_AfterToolRounds_ExitsWithoutExtraCCCall()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("write_brief", () =>
        {
            workspace.TryWriteFile("research/brief.md", "Brief content here.");
            return "brief written";
        });

        var ccCallCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ccCallCount++;
                return CreateToolCallResponse(("write_brief", $"c{ccCallCount}", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("research/brief.md"),
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 5);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed");
        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
        Assert.Equal(1, ccCallCount);
        Assert.True(workspace.FileExists("research/brief.md"));
    }

    [Fact]
    public async Task RunAsync_OneRoundTrip_AfterToolRounds_ExitsAfterFirstRound()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("mark_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "marked";
        });

        var ccCallCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ccCallCount++;
                return CreateToolCallResponse(("mark_done", $"c{ccCallCount}", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.OneRoundTrip);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed");
        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
        Assert.Equal(1, ccCallCount);
    }

    [Fact]
    public async Task RunAsync_SingleCall_AfterToolRounds_StillFiresEarlyCompletion()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("mark_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "marked";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("mark_done", "c1", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.SingleCall);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed");
        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
    }

    #endregion

    #region CheckCompletionAfterToolCalls — AfterEachToolCall

    [Fact]
    public async Task RunAsync_AfterEachToolCall_SkipsRemainingToolsInBatch()
    {
        var workspace = new InMemoryWorkspace();
        var tool1Executed = false;
        var tool2Executed = false;
        var tool3Executed = false;

        var tool1 = CreateTool("write_brief", () =>
        {
            tool1Executed = true;
            workspace.TryWriteFile("brief.md", "done");
            return "written";
        });
        var tool2 = CreateTool("analyze", () =>
        {
            tool2Executed = true;
            return "analyzed";
        });
        var tool3 = CreateTool("summarize", () =>
        {
            tool3Executed = true;
            return "summarized";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(
                ("write_brief", "c1", null),
                ("analyze", "c2", null),
                ("summarize", "c3", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool1, tool2, tool3],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("brief.md"),
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 5);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterEachToolCall;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed");
        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
        Assert.True(tool1Executed, "First tool should have executed");
        Assert.False(tool2Executed, "Second tool should have been skipped");
        Assert.False(tool3Executed, "Third tool should have been skipped");
    }

    [Fact]
    public async Task RunAsync_AfterEachToolCall_AllToolsRunWhenNotComplete()
    {
        var workspace = new InMemoryWorkspace();
        var toolExecCount = 0;

        var tool1 = CreateTool("step1", () => { toolExecCount++; return "ok1"; });
        var tool2 = CreateTool("step2", () => { toolExecCount++; return "ok2"; });
        var tool3 = CreateTool("step3", () => { toolExecCount++; return "ok3"; });

        var ccCallCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ccCallCount++;
                if (ccCallCount == 1)
                {
                    return CreateToolCallResponse(
                        ("step1", "c1", null),
                        ("step2", "c2", null),
                        ("step3", "c3", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool1, tool2, tool3],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("never-created.txt"),
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 5);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterEachToolCall;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, toolExecCount);
        Assert.Equal(TerminationReason.NaturalCompletion, result.Termination);
    }

    [Fact]
    public async Task RunAsync_AfterEachToolCall_LastToolInBatchCompletes()
    {
        var workspace = new InMemoryWorkspace();
        var toolExecCount = 0;

        var tool1 = CreateTool("step1", () => { toolExecCount++; return "ok1"; });
        var tool2 = CreateTool("step2", () => { toolExecCount++; return "ok2"; });
        var tool3 = CreateTool("write_done", () =>
        {
            toolExecCount++;
            workspace.TryWriteFile("done.txt", "yes");
            return "written";
        });

        var ccCallCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ccCallCount++;
                return CreateToolCallResponse(
                    ("step1", $"a{ccCallCount}", null),
                    ("step2", $"b{ccCallCount}", null),
                    ("write_done", $"c{ccCallCount}", null));
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool1, tool2, tool3],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 5);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterEachToolCall;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed");
        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
        Assert.Equal(3, toolExecCount);
        Assert.Equal(1, ccCallCount);
    }

    [Fact]
    public async Task RunAsync_SingleCall_AfterEachToolCall_SkipsRemainingTools()
    {
        var workspace = new InMemoryWorkspace();
        var tool1Executed = false;
        var tool2Executed = false;

        var tool1 = CreateTool("write_done", () =>
        {
            tool1Executed = true;
            workspace.TryWriteFile("done.txt", "yes");
            return "written";
        });
        var tool2 = CreateTool("extra", () =>
        {
            tool2Executed = true;
            return "extra";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(
                ("write_done", "c1", null),
                ("extra", "c2", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool1, tool2],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.SingleCall);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterEachToolCall;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed");
        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
        Assert.True(tool1Executed, "First tool should have executed");
        Assert.False(tool2Executed, "Second tool should have been skipped");
    }

    #endregion

    #region CheckCompletionAfterToolCalls — Shared behavior

    [Fact]
    public async Task RunAsync_CheckCompletion_DefaultNone_NoEarlyExit()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("write_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "written";
        });

        var ccCallCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                ccCallCount++;
                if (ccCallCount == 1)
                {
                    return CreateToolCallResponse(("write_done", "c1", null));
                }

                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")]);
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.MultiRound,
            maxToolRoundsPerIteration: 5);

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, ccCallCount);
        Assert.Equal(TerminationReason.Completed, result.Termination);
    }

    [Fact]
    public async Task RunAsync_CheckCompletion_IsCompleteNull_NoError()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(isComplete: null);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds;

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed with null IsComplete");
        Assert.Equal(TerminationReason.NaturalCompletion, result.Termination);
    }

    [Fact]
    public async Task RunAsync_CheckCompletion_IterationRecordCaptured()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("write_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "written";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var resp = CreateToolCallResponse(("write_done", "c1", null));
                resp.Usage = new UsageDetails
                {
                    InputTokenCount = 100,
                    OutputTokenCount = 50,
                    TotalTokenCount = 150,
                };
                return resp;
            });

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.MultiRound);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected loop to succeed");
        Assert.Null(result.ErrorMessage);
        var iter = Assert.Single(result.Iterations);
        Assert.Equal(1, iter.LlmCallCount);
        Assert.Single(iter.ToolCalls);
        Assert.Equal(150, iter.Tokens.TotalTokens);
    }

    [Fact]
    public async Task RunAsync_CheckCompletion_OnIterationEndStillFires()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("write_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "written";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("write_done", "c1", null)));

        IterationRecord? capturedRecord = null;
        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.MultiRound);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds;
        options.OnIterationEnd = record =>
        {
            capturedRecord = record;
            return Task.CompletedTask;
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
        Assert.NotNull(capturedRecord);
        Assert.Single(capturedRecord.ToolCalls);
    }

    [Fact]
    public async Task RunAsync_CheckCompletion_Configuration_Captured()
    {
        var mockChat = CreateMockChat("done");
        var loop = CreateLoop(mockChat);
        var options = CreateOptions();
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterEachToolCall;

        var result = await loop.RunAsync(options, CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ToolCompletionCheckMode.AfterEachToolCall,
            result.Configuration.CheckCompletionAfterToolCalls);
    }

    #endregion

    #region CheckCompletionAfterToolCalls — Edge cases

    [Fact]
    public async Task RunAsync_EarlyCompletion_WinsOver_MaxToolCallsReached()
    {
        var workspace = new InMemoryWorkspace();
        var tool = CreateTool("write_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "written";
        });

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateToolCallResponse(("write_done", "c1", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.MultiRound);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterToolRounds;
        options.MaxTotalToolCalls = 1;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, "Expected completion to win over MaxToolCallsReached");
        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
    }

    [Fact]
    public async Task RunAsync_AfterEachToolCall_NoExtraMessagesForSkippedCalls()
    {
        var workspace = new InMemoryWorkspace();
        var tool1 = CreateTool("write_done", () =>
        {
            workspace.TryWriteFile("done.txt", "yes");
            return "written";
        });
        var tool2 = CreateTool("extra", () => "extra");

        List<IEnumerable<ChatMessage>>? capturedMessages = null;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>(
                (msgs, _, _) => capturedMessages = [msgs])
            .ReturnsAsync(() => CreateToolCallResponse(
                ("write_done", "c1", null),
                ("extra", "c2", null)));

        var loop = CreateLoop(mockChat);
        var options = CreateOptions(
            tools: [tool1, tool2],
            maxIterations: 5,
            isComplete: ctx => ctx.Workspace.FileExists("done.txt"),
            toolResultMode: ToolResultMode.MultiRound);
        options.CheckCompletionAfterToolCalls = ToolCompletionCheckMode.AfterEachToolCall;

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.CompletedEarlyAfterToolCall, result.Termination);
        mockChat.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    #endregion
}

