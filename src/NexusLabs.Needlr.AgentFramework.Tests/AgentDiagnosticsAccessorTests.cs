using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentDiagnosticsAccessorTests
{
    // -------------------------------------------------------------------------
    // No scope — LastRunDiagnostics is null
    // -------------------------------------------------------------------------

    [Fact]
    public void LastRunDiagnostics_WithoutScope_ReturnsNull()
    {
        var accessor = new AgentDiagnosticsAccessor();

        Assert.Null(accessor.LastRunDiagnostics);
    }

    // -------------------------------------------------------------------------
    // BeginCapture + Set — diagnostics visible
    // -------------------------------------------------------------------------

    [Fact]
    public void Set_InsideScope_MakesDiagnosticsVisible()
    {
        var accessor = new AgentDiagnosticsAccessor();
        var diag = CreateDiagnostics("TestAgent");

        using (accessor.BeginCapture())
        {
            accessor.Set(diag);

            Assert.Same(diag, accessor.LastRunDiagnostics);
        }
    }

    // -------------------------------------------------------------------------
    // Dispose — restores previous scope
    // -------------------------------------------------------------------------

    [Fact]
    public void Dispose_RestoresPreviousScope()
    {
        var accessor = new AgentDiagnosticsAccessor();
        var outerDiag = CreateDiagnostics("Outer");
        var innerDiag = CreateDiagnostics("Inner");

        using (accessor.BeginCapture())
        {
            accessor.Set(outerDiag);

            using (accessor.BeginCapture())
            {
                accessor.Set(innerDiag);
                Assert.Same(innerDiag, accessor.LastRunDiagnostics);
            }

            // Outer scope's diagnostics restored
            Assert.Same(outerDiag, accessor.LastRunDiagnostics);
        }

        // No scope
        Assert.Null(accessor.LastRunDiagnostics);
    }

    // -------------------------------------------------------------------------
    // Mutable holder — child async writes visible to parent
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Set_FromChildAsyncFlow_IsVisibleToParent()
    {
        var accessor = new AgentDiagnosticsAccessor();

        using (accessor.BeginCapture())
        {
            // Simulate middleware setting diagnostics from a child async flow
            await Task.Run(() =>
            {
                var diag = CreateDiagnostics("MiddlewareAgent");
                accessor.Set(diag);
            }, TestContext.Current.CancellationToken);

            // Parent should see the diagnostics set by the child
            Assert.NotNull(accessor.LastRunDiagnostics);
            Assert.Equal("MiddlewareAgent", accessor.LastRunDiagnostics!.AgentName);
        }
    }

    // -------------------------------------------------------------------------
    // AsyncLocal isolation between concurrent captures
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentCaptures_AreIsolated()
    {
        var accessor = new AgentDiagnosticsAccessor();
        var ct = TestContext.Current.CancellationToken;

        string? agentInTask1 = null;
        string? agentInTask2 = null;

        var task1 = Task.Run(() =>
        {
            using (accessor.BeginCapture())
            {
                accessor.Set(CreateDiagnostics("Agent-A"));
                Thread.Sleep(10);
                agentInTask1 = accessor.LastRunDiagnostics?.AgentName;
            }
        }, ct);

        var task2 = Task.Run(() =>
        {
            using (accessor.BeginCapture())
            {
                accessor.Set(CreateDiagnostics("Agent-B"));
                Thread.Sleep(10);
                agentInTask2 = accessor.LastRunDiagnostics?.AgentName;
            }
        }, ct);

        await Task.WhenAll(task1, task2);

        Assert.Equal("Agent-A", agentInTask1);
        Assert.Equal("Agent-B", agentInTask2);
    }

    // -------------------------------------------------------------------------
    // Builder tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Builder_Build_ProducesCorrectDiagnostics()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("TestAgent");

        builder.RecordInputMessageCount(3);
        builder.AddChatCompletion(new ChatCompletionDiagnostics(
            Sequence: 0,
            Model: "gpt-4",
            Tokens: new TokenUsage(10, 20, 30, 5, 0),
            InputMessageCount: 3,
            Duration: TimeSpan.FromMilliseconds(100),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow));

        builder.AddToolCall(new ToolCallDiagnostics(
            Sequence: 0,
            ToolName: "GetData",
            Duration: TimeSpan.FromMilliseconds(50),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null));

        builder.RecordOutputMessageCount(2);

        var result = builder.Build();

        Assert.Equal("TestAgent", result.AgentName);
        Assert.Equal(3, result.TotalInputMessages);
        Assert.Equal(2, result.TotalOutputMessages);
        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.ChatCompletions);
        Assert.Single(result.ToolCalls);
        Assert.Equal(10, result.AggregateTokenUsage.InputTokens);
        Assert.Equal(20, result.AggregateTokenUsage.OutputTokens);
        Assert.Equal(30, result.AggregateTokenUsage.TotalTokens);
    }

    [Fact]
    public void Builder_RecordFailure_SetsFailedState()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("FailAgent");

        builder.RecordFailure("Something went wrong");

        var result = builder.Build();

        Assert.False(result.Succeeded);
        Assert.Equal("Something went wrong", result.ErrorMessage);
    }

    [Fact]
    public void Builder_MultipleCompletions_AreOrderedBySequence()
    {
        using var builder = AgentRunDiagnosticsBuilder.StartNew("OrderAgent");

        // Add in reverse order
        builder.AddChatCompletion(CreateCompletion(sequence: 2));
        builder.AddChatCompletion(CreateCompletion(sequence: 0));
        builder.AddChatCompletion(CreateCompletion(sequence: 1));

        var result = builder.Build();

        Assert.Equal(3, result.ChatCompletions.Count);
        Assert.Equal(0, result.ChatCompletions[0].Sequence);
        Assert.Equal(1, result.ChatCompletions[1].Sequence);
        Assert.Equal(2, result.ChatCompletions[2].Sequence);
    }

    // -------------------------------------------------------------------------
    // DI registration
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingAgentFramework_RegistersDiagnosticsAccessor()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var sp = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
            .BuildServiceProvider(config);

        Assert.NotNull(sp.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(sp.GetService<IToolMetricsAccessor>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IAgentRunDiagnostics CreateDiagnostics(string agentName) =>
        new AgentRunDiagnostics(
            AgentName: agentName,
            TotalDuration: TimeSpan.FromSeconds(1),
            AggregateTokenUsage: new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions: [],
            ToolCalls: [],
            TotalInputMessages: 1,
            TotalOutputMessages: 1,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

    private static ChatCompletionDiagnostics CreateCompletion(int sequence) =>
        new(Sequence: sequence,
            Model: "test",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 0,
            Duration: TimeSpan.Zero,
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);
}
