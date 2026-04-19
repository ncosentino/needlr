using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Tests;

internal static class TeeDiagnosticsSinkTestsHelpers
{
    internal static ChatCompletionDiagnostics MakeCompletion(int sequence) =>
        new(Sequence: sequence,
            Model: "test-model",
            Tokens: new TokenUsage(0, 0, 0, 0, 0),
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
        private int _chatSeq;
        private int _toolSeq;

        public List<ChatCompletionDiagnostics> ChatCompletions { get; } = [];
        public List<ToolCallDiagnostics> ToolCalls { get; } = [];
        public string? AgentName => "Fake";

        public int NextChatCompletionSequence() =>
            Interlocked.Increment(ref _chatSeq) - 1;

        public int NextToolCallSequence() =>
            Interlocked.Increment(ref _toolSeq) - 1;

        public void AddChatCompletion(ChatCompletionDiagnostics diagnostics) =>
            ChatCompletions.Add(diagnostics);

        public void AddToolCall(ToolCallDiagnostics diagnostics) =>
            ToolCalls.Add(diagnostics);
    }

    internal sealed class ThrowingSink : IDiagnosticsSink
    {
        public string? AgentName => "Throwing";
        public int NextChatCompletionSequence() => 0;
        public int NextToolCallSequence() => 0;
        public void AddChatCompletion(ChatCompletionDiagnostics _) => throw new InvalidOperationException("Sink failure");
        public void AddToolCall(ToolCallDiagnostics _) => throw new InvalidOperationException("Sink failure");
    }
}
