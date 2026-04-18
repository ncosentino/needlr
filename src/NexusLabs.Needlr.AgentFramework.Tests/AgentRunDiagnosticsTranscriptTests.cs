using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentRunDiagnosticsTranscriptTests
{
    [Fact]
    public void ToTranscriptMarkdown_NullDiagnostics_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => AgentRunDiagnosticsTranscriptExtensions.ToTranscriptMarkdown(null!));
    }

    [Fact]
    public void ToTranscriptMarkdown_EmptyDiagnostics_RendersHeaderOnly()
    {
        var diag = new StubDiagnostics();

        var md = diag.ToTranscriptMarkdown();

        Assert.Contains("# Agent run: stub-agent", md);
        Assert.Contains("- Execution mode: live", md);
        Assert.Contains("- Succeeded: true", md);
        Assert.DoesNotContain("## Error", md);
        Assert.DoesNotContain("## Input messages", md);
        Assert.DoesNotContain("## Output response", md);
        Assert.Contains("## Timeline", md);
        Assert.Contains("_No diagnostics captured._", md);
    }

    [Fact]
    public void ToTranscriptMarkdown_ChatCompletion_RendersModelTokensCharCountsDuration()
    {
        var startedAt = DateTimeOffset.UnixEpoch;
        var chat = new ChatCompletionDiagnostics(
            Sequence: 0,
            Model: "gpt-test",
            Tokens: new TokenUsage(10, 20, 30, 0, 0),
            InputMessageCount: 2,
            Duration: TimeSpan.FromMilliseconds(123),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: startedAt,
            CompletedAt: startedAt.AddMilliseconds(123))
        {
            RequestCharCount = 456,
            ResponseCharCount = 789,
        };

        var diag = new StubDiagnostics { Chats = [chat] };

        var md = diag.ToTranscriptMarkdown();

        Assert.Contains("### [+0 ms] Chat completion #0", md);
        Assert.Contains("- Model: gpt-test", md);
        Assert.Contains("- Duration: 123 ms", md);
        Assert.Contains("- Tokens: input=10, output=20, total=30", md);
        Assert.Contains("- Request chars: 456", md);
        Assert.Contains("- Response chars: 789", md);
    }

    [Fact]
    public void ToTranscriptMarkdown_ToolCall_RendersNameJsonArgsResultCharCountsDuration()
    {
        var startedAt = DateTimeOffset.UnixEpoch.AddSeconds(1);
        var tool = new ToolCallDiagnostics(
            Sequence: 7,
            ToolName: "get_weather",
            Duration: TimeSpan.FromMilliseconds(42),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: startedAt,
            CompletedAt: startedAt.AddMilliseconds(42),
            CustomMetrics: null)
        {
            Arguments = new Dictionary<string, object?> { ["city"] = "Seattle" },
            Result = new { temp = 52 },
            ArgumentsCharCount = 18,
            ResultCharCount = 12,
        };

        var diag = new StubDiagnostics
        {
            StartedAt = DateTimeOffset.UnixEpoch,
            Tools = [tool],
        };

        var md = diag.ToTranscriptMarkdown();

        Assert.Contains("### [+1000 ms] Tool call #7: get_weather", md);
        Assert.Contains("- Duration: 42 ms", md);
        Assert.Contains("\"city\": \"Seattle\"", md);
        Assert.Contains("\"temp\": 52", md);
        Assert.Contains("- Arguments chars: 18", md);
        Assert.Contains("- Result chars: 12", md);
    }

    [Fact]
    public void ToTranscriptMarkdown_Failure_RendersErrorSection()
    {
        var diag = new StubDiagnostics
        {
            Succeeded = false,
            ErrorMessage = "boom",
        };

        var md = diag.ToTranscriptMarkdown();

        Assert.Contains("- Succeeded: false", md);
        Assert.Contains("## Error", md);
        Assert.Contains("boom", md);
    }

    [Fact]
    public void ToTranscriptMarkdown_PopulatedInputAndOutput_RendersBothSections()
    {
        var input = new[]
        {
            new ChatMessage(ChatRole.System, "You are helpful."),
            new ChatMessage(ChatRole.User, "Hello."),
        };
        var output = new AgentResponse(new List<ChatMessage>
        {
            new ChatMessage(ChatRole.Assistant, "Hi there."),
        });

        var diag = new StubDiagnostics
        {
            Input = input,
            Output = output,
        };

        var md = diag.ToTranscriptMarkdown();

        Assert.Contains("## Input messages", md);
        Assert.Contains("You are helpful.", md);
        Assert.Contains("Hello.", md);
        Assert.Contains("## Output response", md);
        Assert.Contains("Hi there.", md);
    }

    [Fact]
    public void ToTranscriptMarkdown_UsesInvariantCulture()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var chat = new ChatCompletionDiagnostics(
                Sequence: 0,
                Model: "m",
                Tokens: new TokenUsage(1000, 2000, 3000, 0, 0),
                InputMessageCount: 1,
                Duration: TimeSpan.FromMilliseconds(1234),
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: DateTimeOffset.UnixEpoch,
                CompletedAt: DateTimeOffset.UnixEpoch.AddMilliseconds(1234));

            var diag = new StubDiagnostics { Chats = [chat] };

            var md = diag.ToTranscriptMarkdown();

            Assert.Contains("1234 ms", md);
            Assert.Contains("input=1000, output=2000, total=3000", md);
            Assert.DoesNotContain("1.234", md);
            Assert.DoesNotContain("1,234", md);
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }

    [Fact]
    public void ToTranscriptMarkdown_OrderedTimelineInterleaved_RespectsTimeOrder()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var chat0 = new ChatCompletionDiagnostics(
            Sequence: 0,
            Model: "m",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: t0,
            CompletedAt: t0.AddMilliseconds(5));
        var tool0 = new ToolCallDiagnostics(
            Sequence: 0,
            ToolName: "tool_a",
            Duration: TimeSpan.FromMilliseconds(3),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: t0.AddMilliseconds(10),
            CompletedAt: t0.AddMilliseconds(13),
            CustomMetrics: null);
        var chat1 = new ChatCompletionDiagnostics(
            Sequence: 1,
            Model: "m",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: t0.AddMilliseconds(20),
            CompletedAt: t0.AddMilliseconds(25));

        var diag = new StubDiagnostics
        {
            StartedAt = t0,
            Chats = [chat0, chat1],
            Tools = [tool0],
        };

        var md = diag.ToTranscriptMarkdown();

        var idxChat0 = md.IndexOf("Chat completion #0", StringComparison.Ordinal);
        var idxTool0 = md.IndexOf("Tool call #0: tool_a", StringComparison.Ordinal);
        var idxChat1 = md.IndexOf("Chat completion #1", StringComparison.Ordinal);

        Assert.True(idxChat0 >= 0, "chat0 missing");
        Assert.True(idxTool0 >= 0, "tool0 missing");
        Assert.True(idxChat1 >= 0, "chat1 missing");
        Assert.True(idxChat0 < idxTool0, "chat0 should precede tool0");
        Assert.True(idxTool0 < idxChat1, "tool0 should precede chat1");
    }

    private sealed class StubDiagnostics : IAgentRunDiagnostics
    {
        public string AgentName { get; init; } = "stub-agent";
        public TimeSpan TotalDuration { get; init; } = TimeSpan.FromMilliseconds(100);
        public TokenUsage AggregateTokenUsage { get; init; } = new(0, 0, 0, 0, 0);
        public IReadOnlyList<ChatCompletionDiagnostics> Chats { get; init; } = [];
        public IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions => Chats;
        public IReadOnlyList<ToolCallDiagnostics> Tools { get; init; } = [];
        public IReadOnlyList<ToolCallDiagnostics> ToolCalls => Tools;
        public int TotalInputMessages => Input.Count;
        public int TotalOutputMessages => Output?.Messages.Count ?? 0;
        public IReadOnlyList<ChatMessage> Input { get; init; } = [];
        public IReadOnlyList<ChatMessage> InputMessages => Input;
        public AgentResponse? Output { get; init; }
        public AgentResponse? OutputResponse => Output;
        public bool Succeeded { get; init; } = true;
        public string? ErrorMessage { get; init; }
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UnixEpoch;
        public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UnixEpoch.AddMilliseconds(100);
        public string? ExecutionMode { get; init; } = "live";
    }
}
