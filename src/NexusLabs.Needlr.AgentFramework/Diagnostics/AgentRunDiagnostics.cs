using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IAgentRunDiagnostics"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed record AgentRunDiagnostics(
    string AgentName,
    TimeSpan TotalDuration,
    TokenUsage AggregateTokenUsage,
    IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions,
    IReadOnlyList<ToolCallDiagnostics> ToolCalls,
    int TotalInputMessages,
    int TotalOutputMessages,
    IReadOnlyList<ChatMessage> InputMessages,
    AgentResponse? OutputResponse,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string? ExecutionMode = null) : IAgentRunDiagnostics;
