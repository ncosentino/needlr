using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for budget integration, termination reasons, and tool-call guards
/// in <see cref="IterativeAgentLoop"/>.
/// </summary>
public sealed class IterativeAgentLoopBudgetTests
{
    [Fact]
    public async Task RunAsync_BudgetPressure_InjectsInstructionAndTerminates()
    {
        var tracker = new TokenBudgetTracker();
        var prompts = new List<string>();
        int callCount = 0;

        // Mock returns tool calls for the first several calls, then text after
        // budget pressure. Each call records 300 tokens.
        var searchTool = AIFunctionFactory.Create(() => "search result", "search");

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ChatMessage> msgs, ChatOptions? _, CancellationToken _) =>
            {
                callCount++;
                tracker.Record(200, 100);

                // Check if any message contains the pressure instruction
                var hasPresure = msgs.Any(m =>
                    m.Contents.OfType<TextContent>().Any(t =>
                        t.Text?.Contains("FINALIZE NOW") == true));

                if (hasPresure)
                {
                    // After pressure instruction, return text (finalize)
                    return new ChatResponse(
                        [new ChatMessage(ChatRole.Assistant, "Finalized.")])
                    {
                        Usage = new UsageDetails { InputTokenCount = 200, OutputTokenCount = 100, TotalTokenCount = 300 },
                    };
                }

                // Otherwise return a tool call
                return new ChatResponse(
                [
                    new ChatMessage(ChatRole.Assistant,
                    [
                        new FunctionCallContent($"c{callCount}", "search",
                            new Dictionary<string, object?> { ["q"] = "test" }),
                    ]),
                ])
                {
                    Usage = new UsageDetails { InputTokenCount = 200, OutputTokenCount = 100, TotalTokenCount = 300 },
                };
            });

        var loop = CreateLoop(mockChat, budgetTracker: tracker);
        var workspace = new InMemoryWorkspace();

        var options = new IterativeLoopOptions
        {
            Instructions = "Test instructions",
            Tools = [searchTool],
            PromptFactory = ctx =>
            {
                var prompt = $"Iteration {ctx.Iteration}";
                prompts.Add(prompt);
                return prompt;
            },
            MaxIterations = 10,
            ToolResultMode = ToolResultMode.OneRoundTrip,
            BudgetPressureThreshold = 0.8,
            BudgetPressureInstruction = "FINALIZE NOW",
        };

        using (tracker.BeginScope(1000))
        {
            var result = await loop.RunAsync(
                options,
                new IterativeContext { Workspace = workspace },
                TestContext.Current.CancellationToken);

            // After pressure threshold (800 tokens), the instruction is injected
            // and the model returns text, which terminates the loop.
            // BudgetPressure takes priority over NaturalCompletion.
            Assert.Equal(TerminationReason.BudgetPressure, result.Termination);
            Assert.True(result.Iterations.Count >= 2, $"Expected at least 2 iterations, got {result.Iterations.Count}");
        }
    }

    [Fact]
    public async Task RunAsync_MaxTotalToolCalls_TerminatesWithCorrectReason()
    {
        var mockChat = CreateToolCallingChat("search", maxCalls: 20);
        var searchTool = AIFunctionFactory.Create(() => "result", "search");

        var loop = CreateLoop(mockChat);
        var workspace = new InMemoryWorkspace();

        var options = new IterativeLoopOptions
        {
            Instructions = "Search for things",
            Tools = [searchTool],
            PromptFactory = _ => "Search",
            MaxIterations = 100,
            MaxTotalToolCalls = 5,
            ToolResultMode = ToolResultMode.OneRoundTrip,
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.MaxToolCallsReached, result.Termination);
        Assert.False(result.Succeeded);
        Assert.Contains("MaxTotalToolCalls", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_MaxIterationsReached_SetsCorrectTermination()
    {
        var callCount = 0;
        var mockChat = CreateToolCallingChat("search", maxCalls: 100);
        var searchTool = AIFunctionFactory.Create(() => "result", "search");

        var loop = CreateLoop(mockChat);
        var workspace = new InMemoryWorkspace();

        var options = new IterativeLoopOptions
        {
            Instructions = "Keep searching",
            Tools = [searchTool],
            PromptFactory = _ => { callCount++; return "Search more"; },
            MaxIterations = 3,
            ToolResultMode = ToolResultMode.OneRoundTrip,
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.MaxIterationsReached, result.Termination);
        Assert.False(result.Succeeded);
        Assert.Equal(3, result.Iterations.Count);
    }

    [Fact]
    public async Task RunAsync_IsCompleteReturnsTrue_SetsCompletedTermination()
    {
        var mockChat = CreateToolCallingChat("search", maxCalls: 100);
        var searchTool = AIFunctionFactory.Create(() => "result", "search");

        var loop = CreateLoop(mockChat);
        var workspace = new InMemoryWorkspace();
        workspace.WriteFile("done.txt", "");

        var options = new IterativeLoopOptions
        {
            Instructions = "Search",
            Tools = [searchTool],
            PromptFactory = _ => "Go",
            MaxIterations = 10,
            IsComplete = ctx => ctx.Workspace.FileExists("done.txt"),
            ToolResultMode = ToolResultMode.OneRoundTrip,
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.Completed, result.Termination);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_NaturalCompletion_SetsCorrectTermination()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, "I'm done")]));

        var loop = CreateLoop(mockChat);
        var workspace = new InMemoryWorkspace();

        var options = new IterativeLoopOptions
        {
            Instructions = "Do something",
            Tools = [],
            PromptFactory = _ => "Go",
            MaxIterations = 10,
        };

        var result = await loop.RunAsync(
            options,
            new IterativeContext { Workspace = workspace },
            TestContext.Current.CancellationToken);

        Assert.Equal(TerminationReason.NaturalCompletion, result.Termination);
        Assert.True(result.Succeeded);
        Assert.Equal("I'm done", result.FinalResponse);
    }

    [Fact]
    public async Task RunAsync_ChildScope_RollsUpToParent()
    {
        var tracker = new TokenBudgetTracker();
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                tracker.Record(100, 50);
                return new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, "done")])
                {
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 100,
                        OutputTokenCount = 50,
                        TotalTokenCount = 150,
                    },
                };
            });

        var loop = CreateLoop(mockChat, budgetTracker: tracker);
        var workspace = new InMemoryWorkspace();

        var options = new IterativeLoopOptions
        {
            Instructions = "Work",
            Tools = [],
            PromptFactory = _ => "Go",
            MaxIterations = 5,
        };

        using (tracker.BeginScope(10000))
        {
            using (tracker.BeginChildScope("stage-1", 5000))
            {
                await loop.RunAsync(
                    options,
                    new IterativeContext { Workspace = workspace },
                    TestContext.Current.CancellationToken);
            }

            // Parent should see the child's usage rolled up
            Assert.True(tracker.CurrentTokens > 0, "Parent should have tokens from child");
        }
    }

    #region Helpers

    private static IterativeAgentLoop CreateLoop(
        Mock<IChatClient> mockChat,
        ITokenBudgetTracker? budgetTracker = null)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(mockChat.Object);
        return new IterativeAgentLoop(
            accessor.Object,
            diagnosticsWriter: null,
            executionContextAccessor: null,
            progressReporterAccessor: null,
            budgetTracker: budgetTracker);
    }

    private static Mock<IChatClient> CreateToolCallingChat(string toolName, int maxCalls)
    {
        int callNum = 0;
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callNum++;
                if (callNum > maxCalls)
                {
                    return new ChatResponse(
                        [new ChatMessage(ChatRole.Assistant, "done")]);
                }

                return new ChatResponse(
                [
                    new ChatMessage(ChatRole.Assistant,
                    [
                        new FunctionCallContent($"call_{callNum}", toolName,
                            new Dictionary<string, object?> { ["q"] = $"query {callNum}" }),
                    ]),
                ]);
            });
        return mock;
    }

    #endregion
}
