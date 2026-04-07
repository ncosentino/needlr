namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Token usage breakdown for a single LLM call or aggregate across an agent run.
/// </summary>
public sealed record TokenUsage(
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    long CachedInputTokens,
    long ReasoningTokens);
