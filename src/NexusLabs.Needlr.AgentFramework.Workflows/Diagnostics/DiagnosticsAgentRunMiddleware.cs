using System.Diagnostics;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Outermost middleware layer: wraps <c>agent.RunAsync()</c> to capture per-run diagnostics
/// including total duration, message counts, and success/failure state. Emits
/// <see cref="IAgentMetrics"/> counters on start and completion.
/// </summary>
/// <remarks>
/// Streaming <c>RunStreamingAsync()</c> logs a warning because diagnostics capture is
/// not supported for streaming calls. This prevents silent diagnostic loss.
/// </remarks>
internal sealed partial class DiagnosticsAgentRunMiddleware
{
    private readonly string _agentName;
    private readonly IAgentDiagnosticsWriter _writer;
    private readonly IAgentMetrics _metrics;
    private readonly ILogger _logger;

    internal DiagnosticsAgentRunMiddleware(
        string agentName,
        IAgentDiagnosticsWriter writer,
        IAgentMetrics metrics,
        ILogger logger)
    {
        _agentName = agentName;
        _writer = writer;
        _metrics = metrics;
        _logger = logger;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Agent '{AgentName}' is using RunStreamingAsync with diagnostics configured. " +
                  "Streaming calls bypass diagnostics capture — tool calls and chat completions " +
                  "will not be recorded. Switch to RunAsync for full diagnostics.")]
    private partial void LogStreamingDiagnosticsSkipped(string agentName);

    internal IAsyncEnumerable<AgentResponseUpdate> HandleStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var resolvedName = !string.IsNullOrEmpty(innerAgent.Name) ? innerAgent.Name : _agentName;
        LogStreamingDiagnosticsSkipped(resolvedName);
        return innerAgent.RunStreamingAsync(messages, session, options, cancellationToken);
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
        using var activity = _metrics.ActivitySource.StartActivity($"agent.run {resolvedName}", ActivityKind.Internal);
        activity?.SetTag("gen_ai.agent.name", resolvedName);

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
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            var diagnostics = builder.Build();
            _writer.Set(diagnostics);
            _metrics.RecordRunCompleted(diagnostics);

            activity?.SetTag("status", diagnostics.Succeeded ? "success" : "failed");
            activity?.SetTag("gen_ai.usage.input_tokens", diagnostics.AggregateTokenUsage.InputTokens);
            activity?.SetTag("gen_ai.usage.output_tokens", diagnostics.AggregateTokenUsage.OutputTokens);
            activity?.SetTag("gen_ai.usage.total_tokens", diagnostics.AggregateTokenUsage.TotalTokens);
        }
    }
}
