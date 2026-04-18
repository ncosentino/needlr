using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;

/// <summary>
/// Outermost middleware layer: wraps <c>agent.RunAsync()</c> and
/// <c>agent.RunStreamingAsync()</c> to capture per-run diagnostics including
/// total duration, message counts, and success/failure state. Emits
/// <see cref="IAgentMetrics"/> counters on start and completion.
/// </summary>
/// <remarks>
/// Both the non-streaming and streaming paths produce equivalent
/// <see cref="IAgentRunDiagnostics"/> via <see cref="IAgentDiagnosticsWriter.Set"/>.
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

    internal async IAsyncEnumerable<AgentResponseUpdate> HandleStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var resolvedName = !string.IsNullOrEmpty(innerAgent.Name) ? innerAgent.Name : _agentName;

        _metrics.RecordRunStarted(resolvedName);
        using var activity = _metrics.ActivitySource.StartActivity($"agent.run {resolvedName}", ActivityKind.Internal);
        activity?.SetTag("gen_ai.agent.name", resolvedName);
        activity?.SetTag("gen_ai.agent.streaming", true);

        using var builder = AgentRunDiagnosticsBuilder.StartNew(resolvedName);

        var messageList = messages as ICollection<ChatMessage> ?? messages.ToList();
        builder.RecordInputMessageCount(messageList.Count);

        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        Exception? failure = null;

        var enumerator = innerAgent
            .RunStreamingAsync(messageList, session, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                AgentResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }
                    update = enumerator.Current;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }

                if (!string.IsNullOrEmpty(update.MessageId))
                {
                    messageIds.Add(update.MessageId);
                }

                yield return update;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        if (failure is null)
        {
            builder.RecordOutputMessageCount(messageIds.Count);
        }
        else
        {
            builder.RecordOutputMessageCount(messageIds.Count);
            builder.RecordFailure(failure.Message);
            activity?.SetStatus(ActivityStatusCode.Error, failure.Message);
        }

        var diagnostics = builder.Build();
        _writer.Set(diagnostics);
        _metrics.RecordRunCompleted(diagnostics);

        activity?.SetTag("status", diagnostics.Succeeded ? "success" : "failed");
        activity?.SetTag("gen_ai.usage.input_tokens", diagnostics.AggregateTokenUsage.InputTokens);
        activity?.SetTag("gen_ai.usage.output_tokens", diagnostics.AggregateTokenUsage.OutputTokens);
        activity?.SetTag("gen_ai.usage.total_tokens", diagnostics.AggregateTokenUsage.TotalTokens);

        if (failure is not null)
        {
            throw failure;
        }
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
