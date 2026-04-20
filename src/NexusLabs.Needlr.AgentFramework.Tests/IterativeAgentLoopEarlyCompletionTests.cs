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
}
