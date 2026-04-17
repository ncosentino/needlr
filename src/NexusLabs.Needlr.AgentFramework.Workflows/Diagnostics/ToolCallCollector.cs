using System.Collections.Concurrent;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Thread-safe collector for tool call diagnostics captured by the function-calling
/// middleware. Analogous to <see cref="DiagnosticsChatClientMiddleware"/>'s completion
/// collection, ensuring tool calls are not lost when AsyncLocal builders don't propagate.
/// </summary>
internal sealed class ToolCallCollector : IToolCallCollector
{
    private readonly ConcurrentQueue<ToolCallDiagnostics> _toolCalls = new();

    internal void Add(ToolCallDiagnostics diagnostics) =>
        _toolCalls.Enqueue(diagnostics);

    /// <inheritdoc />
    public IReadOnlyList<ToolCallDiagnostics> DrainToolCalls()
    {
        var results = new List<ToolCallDiagnostics>();
        while (_toolCalls.TryDequeue(out var toolCall))
        {
            results.Add(toolCall);
        }
        return results;
    }
}
