using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

internal static class AgentRunDiagnosticsBuilderSecondarySinkTestsHelpers
{
    internal static ChatCompletionDiagnostics MakeCompletion(int sequence) =>
        new(Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(10, 20, 30, 0, 0),
            InputMessageCount: 1,
            Duration: TimeSpan.FromMilliseconds(10),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow);

    internal static ToolCallDiagnostics MakeToolCall(int sequence) =>
        new(Sequence: sequence,
            ToolName: "test-tool",
            Duration: TimeSpan.FromMilliseconds(5),
            Succeeded: true,
            ErrorMessage: null,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            CustomMetrics: null);

    internal sealed class FakeSink : IDiagnosticsSink
    {
        public List<ChatCompletionDiagnostics> ChatCompletions { get; } = [];
        public List<ToolCallDiagnostics> ToolCalls { get; } = [];
        public string? AgentName => "Fake";
        public int NextChatCompletionSequence() => 0;
        public int NextToolCallSequence() => 0;
        public void AddChatCompletion(ChatCompletionDiagnostics d) => ChatCompletions.Add(d);
        public void AddToolCall(ToolCallDiagnostics d) => ToolCalls.Add(d);
    }

    internal sealed class ThrowingSink : IDiagnosticsSink
    {
        public string? AgentName => "Throwing";
        public int NextChatCompletionSequence() => 0;
        public int NextToolCallSequence() => 0;
        public void AddChatCompletion(ChatCompletionDiagnostics _) => throw new InvalidOperationException("fail");
        public void AddToolCall(ToolCallDiagnostics _) => throw new InvalidOperationException("fail");
    }
}
