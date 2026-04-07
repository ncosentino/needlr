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
    private readonly AgentDiagnosticsAccessor _accessor;
    private readonly IAgentMetrics _metrics;

    internal DiagnosticsAgentRunMiddleware(
        string agentName,
        AgentDiagnosticsAccessor accessor,
        IAgentMetrics metrics)
    {
        _agentName = agentName;
        _accessor = accessor;
        _metrics = metrics;
    }

    internal async Task<AgentResponse> HandleAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        _metrics.RecordRunStarted(_agentName);
        var builder = AgentRunDiagnosticsBuilder.StartNew(_agentName);

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
            _accessor.Set(diagnostics);
            _metrics.RecordRunCompleted(diagnostics);
            AgentRunDiagnosticsBuilder.ClearCurrent();
        }
    }
}
