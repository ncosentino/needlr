using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Outermost middleware layer: wraps <c>agent.RunAsync()</c> to capture per-run diagnostics
/// including total duration, message counts, and success/failure state.
/// </summary>
/// <remarks>
/// Streaming <c>RunStreamingAsync()</c> passes through without diagnostics capture —
/// the same approach taken by <see cref="Middleware.AgentResiliencePlugin"/>.
/// </remarks>
internal sealed class DiagnosticsAgentRunMiddleware
{
    private readonly string _agentName;
    private readonly AgentDiagnosticsAccessor _accessor;

    internal DiagnosticsAgentRunMiddleware(string agentName, AgentDiagnosticsAccessor accessor)
    {
        _agentName = agentName;
        _accessor = accessor;
    }

    internal async Task<AgentResponse> HandleAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
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
            AgentRunDiagnosticsBuilder.ClearCurrent();
        }
    }
}
