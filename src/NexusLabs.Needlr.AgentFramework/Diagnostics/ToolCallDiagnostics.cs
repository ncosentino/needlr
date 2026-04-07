namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Diagnostics for a single tool/function invocation within an agent run.
/// </summary>
public sealed record ToolCallDiagnostics(
    int Sequence,
    string ToolName,
    TimeSpan Duration,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyDictionary<string, object?>? CustomMetrics);
