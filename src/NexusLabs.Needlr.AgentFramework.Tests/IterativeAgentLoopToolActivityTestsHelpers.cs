using System.Diagnostics;

using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

internal static class IterativeAgentLoopToolActivityTestsHelpers
{
    internal static ActivityListener CreateListener(string sourceName, List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    internal static IterativeAgentLoop CreateLoop(
        Mock<IChatClient> mockChat,
        IAgentMetrics? metrics = null)
    {
        var accessor = new Mock<IChatClientAccessor>();
        accessor.Setup(a => a.ChatClient).Returns(mockChat.Object);
        return new IterativeAgentLoop(accessor.Object, metrics: metrics);
    }

    internal static IterativeContext CreateContext() =>
        new() { Workspace = new InMemoryWorkspace() };

    internal static IterativeLoopOptions CreateOptions(IReadOnlyList<AITool> tools) =>
        new()
        {
            Instructions = "You are a test assistant.",
            PromptFactory = _ => "Do something.",
            Tools = tools,
            MaxIterations = 2,
            IsComplete = _ => true,
            LoopName = "test-activity-loop",
        };

    internal static Mock<IChatClient> CreateToolCallThenDoneChat(string toolName)
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
                var count = Interlocked.Increment(ref callCount);
                if (count == 1)
                {
                    return new ChatResponse(
                    [
                        new ChatMessage(ChatRole.Assistant,
                        [
                            new FunctionCallContent("call-1", toolName,
                                new Dictionary<string, object?> { ["arg1"] = "val1" })
                        ])
                    ]);
                }

                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "done")]);
            });
        return mock;
    }

    internal static AIFunction CreateTool(string name, Func<object?> execute)
    {
        return AIFunctionFactory.Create(
            () => execute(),
            new AIFunctionFactoryOptions { Name = name });
    }
}
