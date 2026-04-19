using System.Diagnostics;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using static NexusLabs.Needlr.AgentFramework.Tests.IterativeLoopDiagnosticsIntegrationTestsHelpers;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// End-to-end deduplication tests that wire the full DI pipeline
/// (<c>UsingAgentFramework</c> + <c>UsingDiagnostics</c>) and verify that
/// <see cref="IIterativeAgentLoop"/> produces correct, non-duplicated diagnostics.
/// </summary>
public sealed class IterativeLoopDiagnosticsIntegrationTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // C1: DI-resolved loop receives IAgentMetrics
    [Fact]
    public void DI_IterativeAgentLoop_ReceivesIAgentMetrics()
    {
        var sp = BuildServiceProvider(CreateMockChat("ok"), useDiagnostics: true);
        var loop = sp.GetRequiredService<IIterativeAgentLoop>();

        // Use reflection to verify the _metrics field is non-null
        var metricsField = loop.GetType().GetField(
            "_metrics",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(metricsField);

        var metricsValue = metricsField!.GetValue(loop);
        Assert.NotNull(metricsValue);
    }

    // C2: DI-resolved loop emits tool call Activity spans
    [Fact]
    public async Task DI_IterativeAgentLoop_EmitsToolCallActivitySpans()
    {
        var sourceName = "NexusLabs.Needlr.AgentFramework";
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        var mockChat = CreateToolCallThenDoneChat("DITestTool");
        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var loop = sp.GetRequiredService<IIterativeAgentLoop>();

        var tool = AIFunctionFactory.Create(() => "result",
            new AIFunctionFactoryOptions { Name = "DITestTool" });

        await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        var toolActivities = activities.Where(a => a.OperationName.StartsWith("agent.tool")).ToList();
        Assert.NotEmpty(toolActivities);
    }

    // D1: UsingDiagnostics + IterativeLoop produces exactly 1 ChatCompletion per call
    [Fact]
    public async Task UsingDiagnostics_PlusIterativeLoop_SingleChatCompletionPerCall()
    {
        var mockChat = CreateToolCallThenDoneChat("ResearchTool");
        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var loop = sp.GetRequiredService<IIterativeAgentLoop>();

        var tool = AIFunctionFactory.Create(() => "research result",
            new AIFunctionFactoryOptions { Name = "ResearchTool" });

        var result = await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        // 2 actual LLM calls: tool call round + final text
        Assert.Equal(2, result.Diagnostics!.ChatCompletions.Count);
    }

    // D2: AggregateTokenUsage matches unique entries
    [Fact]
    public async Task UsingDiagnostics_PlusIterativeLoop_AggregateTokenUsage_MatchesUnique()
    {
        var mockChat = CreateToolCallThenDoneChat("TokenTool", inputTokens: 500, outputTokens: 250);
        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var loop = sp.GetRequiredService<IIterativeAgentLoop>();

        var tool = AIFunctionFactory.Create(() => "token result",
            new AIFunctionFactoryOptions { Name = "TokenTool" });

        var result = await loop.RunAsync(CreateOptions([tool]), CreateContext(), _ct);

        var expectedTotal = result.Diagnostics!.ChatCompletions
            .Sum(c => c.Tokens.TotalTokens);
        Assert.Equal(expectedTotal, result.Diagnostics!.AggregateTokenUsage.TotalTokens);
    }

    // D3: MultiRound mode, many iterations — no duplication
    [Fact]
    public async Task UsingDiagnostics_PlusIterativeLoop_MultiRound_NoDuplication()
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

                // Iterations 1-6: each returns a tool call, then text response
                // Total: 12 LLM calls (6 tool + 6 text)
                if (n % 2 == 1 && n <= 12)
                {
                    return CreateToolCallResponse("MultiTool", $"call-{n}", 100, 50);
                }

                return CreateTextResponse($"iteration-{n / 2}-done", 80, 40);
            });

        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var loop = sp.GetRequiredService<IIterativeAgentLoop>();

        var tool = AIFunctionFactory.Create(() => "multi result",
            new AIFunctionFactoryOptions { Name = "MultiTool" });

        var options = CreateOptions([tool]);
        options.ToolResultMode = ToolResultMode.MultiRound;
        options.MaxIterations = 6;
        options.IsComplete = null;

        var result = await loop.RunAsync(options, CreateContext(), _ct);

        // Each completion should be unique
        var sequences = result.Diagnostics!.ChatCompletions.Select(c => c.Sequence).ToList();
        Assert.Equal(sequences.Count, sequences.Distinct().Count());

        // Token total should match sum of unique entries
        var expectedTotal = result.Diagnostics!.ChatCompletions
            .Sum(c => c.Tokens.TotalTokens);
        Assert.Equal(expectedTotal, result.Diagnostics!.AggregateTokenUsage.TotalTokens);
    }

    // D4: Without UsingDiagnostics, loop still produces diagnostics
    [Fact]
    public async Task WithoutUsingDiagnostics_IterativeLoop_StillProducesDiagnostics()
    {
        var mockChat = CreateMockChat("response");
        var sp = BuildServiceProvider(mockChat, useDiagnostics: false);
        var loop = sp.GetRequiredService<IIterativeAgentLoop>();

        var result = await loop.RunAsync(CreateOptions([]), CreateContext(), _ct);

        Assert.NotNull(result.Diagnostics);
        Assert.Single(result.Diagnostics!.ChatCompletions);
        Assert.True(result.Diagnostics!.Succeeded, "Run should succeed");
    }

    // D5: Consecutive runs don't cross-contaminate
    [Fact]
    public async Task UsingDiagnostics_PlusIterativeLoop_ConsecutiveRuns_NoCrossContamination()
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
                Interlocked.Increment(ref callCount);
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, $"response-{callCount}")])
                {
                    ModelId = "test-model",
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 10 * callCount,
                        OutputTokenCount = 5 * callCount,
                        TotalTokenCount = 15 * callCount,
                    },
                };
            });

        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var loop = sp.GetRequiredService<IIterativeAgentLoop>();

        // First run
        var result1 = await loop.RunAsync(CreateOptions([]), CreateContext(), _ct);

        // Second run
        var result2 = await loop.RunAsync(CreateOptions([]), CreateContext(), _ct);

        // Each run should have exactly 1 completion
        Assert.Single(result1.Diagnostics!.ChatCompletions);
        Assert.Single(result2.Diagnostics!.ChatCompletions);

        // Token counts should differ (mock increments callCount)
        Assert.NotEqual(
            result1.Diagnostics!.AggregateTokenUsage.TotalTokens,
            result2.Diagnostics!.AggregateTokenUsage.TotalTokens);
    }

}
