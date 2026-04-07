using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Outermost middleware layer: wraps <c>agent.RunAsync()</c> to capture per-run diagnostics
/// including total duration, message counts, and success/failure state. Emits
/// <see cref="IAgentMetrics"/> counters on start and completion.
/// </summary>
/// <remarks>
/// Streaming <c>RunStreamingAsync()</c> passes through without diagnostics capture —
/// the same approach taken by <see cref="Middleware.AgentResiliencePlugin"/>.
/// </remarks>
internal sealed class DiagnosticsAgentRunMiddleware
{
    private readonly string _agentName;
    private readonly IAgentDiagnosticsWriter _writer;
    private readonly IAgentMetrics _metrics;

    internal DiagnosticsAgentRunMiddleware(
        string agentName,
        IAgentDiagnosticsWriter writer,
        IAgentMetrics metrics)
    {
        _agentName = agentName;
        _writer = writer;
        _metrics = metrics;
    }

    internal async Task<AgentResponse> HandleAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        // Resolve the agent name at runtime from the inner agent. The plugin creates
        // this middleware before the agent is fully built, so the name passed at
        // construction time is a fallback.
        var resolvedName = !string.IsNullOrEmpty(innerAgent.Name) ? innerAgent.Name : _agentName;

        _metrics.RecordRunStarted(resolvedName);
        using var builder = AgentRunDiagnosticsBuilder.StartNew(resolvedName);

        try
        {
            var messageList = messages as ICollection<ChatMessage> ?? messages.ToList();
            builder.RecordInputMessageCount(messageList.Count);

            var response = await innerAgent.RunAsync(messageList, session, options, cancellationToken)
                .ConfigureAwait(false);

            builder.RecordOutputMessageCount(response.Messages?.Count ?? 0);

            return response;
        }
        catch (Exception ex)
        {
            builder.RecordFailure(ex.Message);
            throw;
        }
        finally
        {
            var diagnostics = builder.Build();
            _writer.Set(diagnostics);
            _metrics.RecordRunCompleted(diagnostics);
        }
    }
}
