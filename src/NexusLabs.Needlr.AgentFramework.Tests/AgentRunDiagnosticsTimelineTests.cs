using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentRunDiagnosticsTimelineTests
{
    [Fact]
    public void GetOrderedTimeline_EmptyDiagnostics_ReturnsEmpty()
    {
        var diag = BuildDiagnostics([], []);

        var timeline = diag.GetOrderedTimeline();

        Assert.Empty(timeline);
    }

    [Fact]
    public void GetOrderedTimeline_OnlyChatCompletions_PreservesOrder()
    {
        var t0 = DateTimeOffset.UtcNow;
        var chats = new[]
        {
            BuildChat(sequence: 0, startedAt: t0),
            BuildChat(sequence: 1, startedAt: t0.AddMilliseconds(10)),
            BuildChat(sequence: 2, startedAt: t0.AddMilliseconds(20)),
        };

        var diag = BuildDiagnostics(chats, []);

        var timeline = diag.GetOrderedTimeline();

        Assert.Equal(3, timeline.Count);
        Assert.All(timeline, e => Assert.Equal(DiagnosticsTimelineEntryKind.ChatCompletion, e.Kind));
        Assert.Equal([0, 1, 2], timeline.Select(e => e.Sequence));
    }

    [Fact]
    public void GetOrderedTimeline_OnlyToolCalls_PreservesOrder()
    {
        var t0 = DateTimeOffset.UtcNow;
        var tools = new[]
        {
            BuildTool(sequence: 0, startedAt: t0),
            BuildTool(sequence: 1, startedAt: t0.AddMilliseconds(5)),
        };

        var diag = BuildDiagnostics([], tools);

        var timeline = diag.GetOrderedTimeline();

        Assert.Equal(2, timeline.Count);
        Assert.All(timeline, e => Assert.Equal(DiagnosticsTimelineEntryKind.ToolCall, e.Kind));
        Assert.Equal([0, 1], timeline.Select(e => e.Sequence));
    }

    [Fact]
    public void GetOrderedTimeline_MergesAndOrdersByStartedAt()
    {
        var t0 = DateTimeOffset.UtcNow;
        var chats = new[]
        {
            BuildChat(sequence: 0, startedAt: t0),
            BuildChat(sequence: 1, startedAt: t0.AddMilliseconds(30)),
        };
        var tools = new[]
        {
            BuildTool(sequence: 0, startedAt: t0.AddMilliseconds(10)),
            BuildTool(sequence: 1, startedAt: t0.AddMilliseconds(20)),
        };

        var diag = BuildDiagnostics(chats, tools);

        var timeline = diag.GetOrderedTimeline();

        Assert.Equal(4, timeline.Count);
        Assert.Equal(DiagnosticsTimelineEntryKind.ChatCompletion, timeline[0].Kind);
        Assert.Equal(DiagnosticsTimelineEntryKind.ToolCall, timeline[1].Kind);
        Assert.Equal(DiagnosticsTimelineEntryKind.ToolCall, timeline[2].Kind);
        Assert.Equal(DiagnosticsTimelineEntryKind.ChatCompletion, timeline[3].Kind);
    }

    [Fact]
    public void GetOrderedTimeline_TiedTimestamps_ChatBeforeTool()
    {
        var t0 = DateTimeOffset.UtcNow;
        var chats = new[] { BuildChat(sequence: 0, startedAt: t0) };
        var tools = new[] { BuildTool(sequence: 0, startedAt: t0) };

        var diag = BuildDiagnostics(chats, tools);

        var timeline = diag.GetOrderedTimeline();

        Assert.Equal(2, timeline.Count);
        Assert.Equal(DiagnosticsTimelineEntryKind.ChatCompletion, timeline[0].Kind);
        Assert.Equal(DiagnosticsTimelineEntryKind.ToolCall, timeline[1].Kind);
    }

    [Fact]
    public void GetOrderedTimeline_ChatCompletionEntry_ExposesSource()
    {
        var t0 = DateTimeOffset.UtcNow;
        var chat = BuildChat(sequence: 7, startedAt: t0);
        var diag = BuildDiagnostics([chat], []);

        var entry = diag.GetOrderedTimeline().Single();

        Assert.Equal(DiagnosticsTimelineEntryKind.ChatCompletion, entry.Kind);
        Assert.Same(chat, entry.ChatCompletion);
        Assert.Null(entry.ToolCall);
        Assert.Equal(7, entry.Sequence);
        Assert.Equal(chat.StartedAt, entry.StartedAt);
        Assert.Equal(chat.CompletedAt, entry.CompletedAt);
    }

    [Fact]
    public void GetOrderedTimeline_ToolCallEntry_ExposesSource()
    {
        var t0 = DateTimeOffset.UtcNow;
        var tool = BuildTool(sequence: 3, startedAt: t0);
        var diag = BuildDiagnostics([], [tool]);

        var entry = diag.GetOrderedTimeline().Single();

        Assert.Equal(DiagnosticsTimelineEntryKind.ToolCall, entry.Kind);
        Assert.Same(tool, entry.ToolCall);
        Assert.Null(entry.ChatCompletion);
        Assert.Equal(3, entry.Sequence);
        Assert.Equal(tool.StartedAt, entry.StartedAt);
        Assert.Equal(tool.CompletedAt, entry.CompletedAt);
    }

    [Fact]
    public void GetOrderedTimeline_NullDiagnostics_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => AgentRunDiagnosticsTimelineExtensions.GetOrderedTimeline(null!));
    }

    private static ChatCompletionDiagnostics BuildChat(
        int sequence,
        DateTimeOffset startedAt)
    {
        return new ChatCompletionDiagnostics(
            Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: startedAt,
            CompletedAt: startedAt.AddMilliseconds(5));
    }

    private static ToolCallDiagnostics BuildTool(
        int sequence,
        DateTimeOffset startedAt)
    {
        return new ToolCallDiagnostics(
            Sequence: sequence,
            ToolName: "test-tool",
            Duration: TimeSpan.FromMilliseconds(3),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: startedAt,
            CompletedAt: startedAt.AddMilliseconds(3),
            CustomMetrics: null);
    }

    private static IAgentRunDiagnostics BuildDiagnostics(
        IReadOnlyList<ChatCompletionDiagnostics> chats,
        IReadOnlyList<ToolCallDiagnostics> tools)
    {
        return new StubAgentRunDiagnostics(chats, tools);
    }

    private sealed class StubAgentRunDiagnostics(
        IReadOnlyList<ChatCompletionDiagnostics> chats,
        IReadOnlyList<ToolCallDiagnostics> tools) : IAgentRunDiagnostics
    {
        public string AgentName => "stub";
        public TimeSpan TotalDuration => TimeSpan.Zero;
        public TokenUsage AggregateTokenUsage => new(0, 0, 0, 0, 0);
        public IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions => chats;
        public IReadOnlyList<ToolCallDiagnostics> ToolCalls => tools;
        public int TotalInputMessages => 0;
        public int TotalOutputMessages => 0;
        public IReadOnlyList<ChatMessage> InputMessages => [];
        public AgentResponse? OutputResponse => null;
        public bool Succeeded => true;
        public string? ErrorMessage => null;
        public DateTimeOffset StartedAt => DateTimeOffset.UnixEpoch;
        public DateTimeOffset CompletedAt => DateTimeOffset.UnixEpoch;
        public string? ExecutionMode => null;
    }
}
