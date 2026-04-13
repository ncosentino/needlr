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
    DateTimeOffset CompletedAt)
{
    /// <summary>
    /// The name of the agent that triggered this completion, or <see langword="null"/>
    /// if the agent name was not available. Used to attribute completions to the
    /// correct stage in group chat workflows where multiple agents share a single
    /// chat client.
    /// </summary>
    public string? AgentName { get; init; }
}
