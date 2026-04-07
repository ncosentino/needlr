namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Diagnostics for a single LLM chat completion call within an agent run.
/// </summary>
public sealed record ChatCompletionDiagnostics(
    int Sequence,
    string Model,
    TokenUsage Tokens,
    int InputMessageCount,
    TimeSpan Duration,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
