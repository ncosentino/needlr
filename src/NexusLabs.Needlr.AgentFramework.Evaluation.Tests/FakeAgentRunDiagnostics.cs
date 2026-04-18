using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

internal sealed class FakeAgentRunDiagnostics : IAgentRunDiagnostics
{
    public required string AgentName { get; init; }
    public required TimeSpan TotalDuration { get; init; }
    public required TokenUsage AggregateTokenUsage { get; init; }
    public required IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions { get; init; }
    public required IReadOnlyList<ToolCallDiagnostics> ToolCalls { get; init; }
    public required int TotalInputMessages { get; init; }
    public required int TotalOutputMessages { get; init; }
    public required IReadOnlyList<ChatMessage> InputMessages { get; init; }
    public required AgentResponse? OutputResponse { get; init; }
    public required bool Succeeded { get; init; }
    public required string? ErrorMessage { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required string? ExecutionMode { get; init; }

    public static FakeAgentRunDiagnostics Create(
        string agentName = "test-agent",
        IReadOnlyList<ChatMessage>? inputMessages = null,
        AgentResponse? outputResponse = null)
    {
        var inputs = inputMessages ?? new List<ChatMessage> { new(ChatRole.User, "Hello.") };
        return new FakeAgentRunDiagnostics
        {
            AgentName = agentName,
            TotalDuration = TimeSpan.FromMilliseconds(1),
            AggregateTokenUsage = new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions = Array.Empty<ChatCompletionDiagnostics>(),
            ToolCalls = Array.Empty<ToolCallDiagnostics>(),
            TotalInputMessages = inputs.Count,
            TotalOutputMessages = outputResponse?.Messages.Count ?? 0,
            InputMessages = inputs,
            OutputResponse = outputResponse,
            Succeeded = true,
            ErrorMessage = null,
            StartedAt = DateTimeOffset.UnixEpoch,
            CompletedAt = DateTimeOffset.UnixEpoch,
            ExecutionMode = null,
        };
    }
}
