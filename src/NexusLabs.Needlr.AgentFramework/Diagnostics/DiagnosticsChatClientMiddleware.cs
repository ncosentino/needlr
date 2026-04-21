using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Single writer for chat completion diagnostics. Wraps each
/// <c>IChatClient.GetResponseAsync()</c> call to capture per-completion timing,
/// token usage, and full request/response payloads. Records to the AsyncLocal
/// <see cref="AgentRunDiagnosticsBuilder"/> and a thread-safe collection (for
/// workflow runs where AsyncLocal doesn't propagate). Optionally emits
/// <see cref="LlmCallStartedEvent"/>/<see cref="LlmCallCompletedEvent"/> to the
/// progress reporter and OTel metrics via <see cref="IAgentMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>IterativeAgentLoop</c> wraps its chat client with this middleware
/// internally, making it the sole writer for <see cref="ChatCompletionDiagnostics"/>.
/// No other code should call <see cref="AgentRunDiagnosticsBuilder.AddChatCompletion"/>
/// for calls that pass through this middleware.
/// </para>
/// <para>
/// <see cref="IAgentMetrics"/> and <see cref="IProgressReporterAccessor"/> are optional.
/// When null, recording still occurs but OTel metrics and progress events are skipped.
/// </para>
/// </remarks>
[DoNotAutoRegister]
internal sealed class DiagnosticsChatClientMiddleware : IChatCompletionCollector
{
    private readonly IAgentMetrics? _metrics;
    private readonly IProgressReporterAccessor? _progressAccessor;
    private readonly ChatCompletionActivityMode _activityMode;
    private readonly ConcurrentQueue<ChatCompletionDiagnostics> _allCompletions = new();
    private int _sequenceCounter;

    internal DiagnosticsChatClientMiddleware(
        IAgentMetrics? metrics = null,
        IProgressReporterAccessor? progressAccessor = null,
        ChatCompletionActivityMode activityMode = ChatCompletionActivityMode.Always)
    {
        _metrics = metrics;
        _progressAccessor = progressAccessor;
        _activityMode = activityMode;
    }

    /// <summary>
    /// Drains all captured completions since the last drain. Thread-safe.
    /// </summary>
    public IReadOnlyList<ChatCompletionDiagnostics> DrainCompletions()
    {
        var results = new List<ChatCompletionDiagnostics>();
        while (_allCompletions.TryDequeue(out var completion))
        {
            results.Add(completion);
        }
        return results;
    }

    internal async Task<ChatResponse> HandleAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        CancellationToken cancellationToken)
    {
        var builder = AgentRunDiagnosticsBuilder.GetCurrent();
        var sequence = builder?.NextChatCompletionSequence()
            ?? Interlocked.Increment(ref _sequenceCounter) - 1;
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        using var activity = StartChatActivity("agent.chat");

        if (_progressAccessor is not null)
        {
            _progressAccessor.Current.Report(new LlmCallStartedEvent(
                Timestamp: startedAt,
                WorkflowId: _progressAccessor.Current.WorkflowId,
                AgentId: _progressAccessor.Current.AgentId,
                ParentAgentId: builder?.ParentAgentName,
                Depth: _progressAccessor.Current.Depth,
                SequenceNumber: _progressAccessor.Current.NextSequence(),
                CallSequence: sequence));
        }

        try
        {
            var response = await innerChatClient.GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            var model = response.ModelId ?? "unknown";

            activity?.SetTag("gen_ai.response.model", model);
            activity?.SetTag("agent.chat.sequence", sequence);
            activity?.SetTag("status", "success");

            _metrics?.RecordChatCompletion(model, stopwatch.Elapsed, succeeded: true, agentName: builder?.AgentName);

            var usage = response.Usage;
            var tokens = new TokenUsage(
                InputTokens: usage?.InputTokenCount ?? 0,
                OutputTokens: usage?.OutputTokenCount ?? 0,
                TotalTokens: usage?.TotalTokenCount ?? 0,
                CachedInputTokens: usage?.AdditionalCounts?.GetValueOrDefault("CachedInputTokens") ?? 0,
                ReasoningTokens: usage?.AdditionalCounts?.GetValueOrDefault("ReasoningTokens") ?? 0);

            activity?.SetTag("gen_ai.usage.input_tokens", tokens.InputTokens);
            activity?.SetTag("gen_ai.usage.output_tokens", tokens.OutputTokens);

            var messageList = messages as ICollection<ChatMessage> ?? messages.ToList();

            var diagnostics = new ChatCompletionDiagnostics(
                Sequence: sequence,
                Model: model,
                Tokens: tokens,
                InputMessageCount: messageList.Count,
                Duration: stopwatch.Elapsed,
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow)
            {
                AgentName = builder?.AgentName,
                RequestMessages = messageList as IReadOnlyList<ChatMessage> ?? messageList.ToList(),
                Response = response,
                RequestCharCount = DiagnosticsCharCounter.ChatMessagesLength(messageList as IReadOnlyList<ChatMessage> ?? messageList.ToList()),
                ResponseCharCount = DiagnosticsCharCounter.ChatResponseLength(response),
            };

            builder?.AddChatCompletion(diagnostics);
            _allCompletions.Enqueue(diagnostics);

            if (_progressAccessor is not null)
            {
                _progressAccessor.Current.Report(new LlmCallCompletedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: _progressAccessor.Current.WorkflowId,
                    AgentId: _progressAccessor.Current.AgentId,
                    ParentAgentId: builder?.ParentAgentName,
                    Depth: _progressAccessor.Current.Depth,
                    SequenceNumber: _progressAccessor.Current.NextSequence(),
                    CallSequence: sequence,
                    Model: model,
                    Duration: stopwatch.Elapsed,
                    InputTokens: tokens.InputTokens,
                    OutputTokens: tokens.OutputTokens,
                    TotalTokens: tokens.TotalTokens));
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("status", "failed");

            _metrics?.RecordChatCompletion("unknown", stopwatch.Elapsed, succeeded: false, agentName: builder?.AgentName);

            var failedMessageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

            var diagnostics = new ChatCompletionDiagnostics(
                Sequence: sequence,
                Model: "unknown",
                Tokens: new TokenUsage(0, 0, 0, 0, 0),
                InputMessageCount: 0,
                Duration: stopwatch.Elapsed,
                Succeeded: false,
                ErrorMessage: ex.Message,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow)
            {
                AgentName = builder?.AgentName,
                RequestMessages = failedMessageList,
                RequestCharCount = DiagnosticsCharCounter.ChatMessagesLength(failedMessageList),
            };

            builder?.AddChatCompletion(diagnostics);
            _allCompletions.Enqueue(diagnostics);

            if (_progressAccessor is not null)
            {
                _progressAccessor.Current.Report(new LlmCallFailedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: _progressAccessor.Current.WorkflowId,
                    AgentId: _progressAccessor.Current.AgentId,
                    ParentAgentId: builder?.ParentAgentName,
                    Depth: _progressAccessor.Current.Depth,
                    SequenceNumber: _progressAccessor.Current.NextSequence(),
                    CallSequence: sequence,
                    ErrorMessage: ex.Message,
                    Duration: stopwatch.Elapsed));
            }

            throw;
        }
    }

    internal async IAsyncEnumerable<ChatResponseUpdate> HandleStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var builder = AgentRunDiagnosticsBuilder.GetCurrent();
        var sequence = builder?.NextChatCompletionSequence()
            ?? Interlocked.Increment(ref _sequenceCounter) - 1;
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        using var activity = StartChatActivity("agent.chat.stream");

        if (_progressAccessor is not null)
        {
            _progressAccessor.Current.Report(new LlmCallStartedEvent(
                Timestamp: startedAt,
                WorkflowId: _progressAccessor.Current.WorkflowId,
                AgentId: _progressAccessor.Current.AgentId,
                ParentAgentId: builder?.ParentAgentName,
                Depth: _progressAccessor.Current.Depth,
                SequenceNumber: _progressAccessor.Current.NextSequence(),
                CallSequence: sequence));
        }

        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var buffered = new List<ChatResponseUpdate>();
        Exception? failure = null;

        var enumerable = innerChatClient.GetStreamingResponseAsync(messages, options, cancellationToken);
        var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                ChatResponseUpdate update;
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

                buffered.Add(update);
                yield return update;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        stopwatch.Stop();

        var aggregated = buffered.ToChatResponse();

        if (failure is null)
        {
            var model = aggregated.ModelId ?? "unknown";

            activity?.SetTag("gen_ai.response.model", model);
            activity?.SetTag("agent.chat.sequence", sequence);
            activity?.SetTag("status", "success");

            _metrics?.RecordChatCompletion(model, stopwatch.Elapsed, succeeded: true, agentName: builder?.AgentName);

            var usage = aggregated.Usage;
            var tokens = new TokenUsage(
                InputTokens: usage?.InputTokenCount ?? 0,
                OutputTokens: usage?.OutputTokenCount ?? 0,
                TotalTokens: usage?.TotalTokenCount ?? 0,
                CachedInputTokens: usage?.AdditionalCounts?.GetValueOrDefault("CachedInputTokens") ?? 0,
                ReasoningTokens: usage?.AdditionalCounts?.GetValueOrDefault("ReasoningTokens") ?? 0);

            activity?.SetTag("gen_ai.usage.input_tokens", tokens.InputTokens);
            activity?.SetTag("gen_ai.usage.output_tokens", tokens.OutputTokens);

            var diagnostics = new ChatCompletionDiagnostics(
                Sequence: sequence,
                Model: model,
                Tokens: tokens,
                InputMessageCount: messageList.Count,
                Duration: stopwatch.Elapsed,
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow)
            {
                AgentName = builder?.AgentName,
                RequestMessages = messageList,
                Response = aggregated,
                RequestCharCount = DiagnosticsCharCounter.ChatMessagesLength(messageList),
                ResponseCharCount = DiagnosticsCharCounter.ChatResponseLength(aggregated),
            };

            builder?.AddChatCompletion(diagnostics);
            _allCompletions.Enqueue(diagnostics);

            if (_progressAccessor is not null)
            {
                _progressAccessor.Current.Report(new LlmCallCompletedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: _progressAccessor.Current.WorkflowId,
                    AgentId: _progressAccessor.Current.AgentId,
                    ParentAgentId: builder?.ParentAgentName,
                    Depth: _progressAccessor.Current.Depth,
                    SequenceNumber: _progressAccessor.Current.NextSequence(),
                    CallSequence: sequence,
                    Model: model,
                    Duration: stopwatch.Elapsed,
                    InputTokens: tokens.InputTokens,
                    OutputTokens: tokens.OutputTokens,
                    TotalTokens: tokens.TotalTokens));
            }
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Error, failure.Message);
            activity?.SetTag("status", "failed");

            _metrics?.RecordChatCompletion("unknown", stopwatch.Elapsed, succeeded: false, agentName: builder?.AgentName);

            var diagnostics = new ChatCompletionDiagnostics(
                Sequence: sequence,
                Model: aggregated.ModelId ?? "unknown",
                Tokens: new TokenUsage(0, 0, 0, 0, 0),
                InputMessageCount: 0,
                Duration: stopwatch.Elapsed,
                Succeeded: false,
                ErrorMessage: failure.Message,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow)
            {
                AgentName = builder?.AgentName,
                RequestMessages = messageList,
                Response = aggregated,
                RequestCharCount = DiagnosticsCharCounter.ChatMessagesLength(messageList),
                ResponseCharCount = DiagnosticsCharCounter.ChatResponseLength(aggregated),
            };

            builder?.AddChatCompletion(diagnostics);
            _allCompletions.Enqueue(diagnostics);

            if (_progressAccessor is not null)
            {
                _progressAccessor.Current.Report(new LlmCallFailedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: _progressAccessor.Current.WorkflowId,
                    AgentId: _progressAccessor.Current.AgentId,
                    ParentAgentId: builder?.ParentAgentName,
                    Depth: _progressAccessor.Current.Depth,
                    SequenceNumber: _progressAccessor.Current.NextSequence(),
                    CallSequence: sequence,
                    ErrorMessage: failure.Message,
                    Duration: stopwatch.Elapsed));
            }

            throw failure;
        }
    }

    /// <summary>
    /// Creates a chat completion activity respecting <see cref="_activityMode"/>.
    /// When <see cref="ChatCompletionActivityMode.EnrichParent"/> is active and a
    /// parent <c>gen_ai.*</c> activity exists, returns <see langword="null"/> (the
    /// caller enriches <see cref="Activity.Current"/> directly via <c>activity?.SetTag</c>
    /// null-checks). When no parent exists, creates a new activity as normal.
    /// </summary>
    private Activity? StartChatActivity(string operationName)
    {
        if (_metrics is null)
        {
            return null;
        }

        if (_activityMode == ChatCompletionActivityMode.EnrichParent)
        {
            var parent = Activity.Current;
            if (parent?.OperationName.StartsWith("gen_ai.", StringComparison.Ordinal) == true)
            {
                return null;
            }
        }

        return _metrics.ActivitySource.StartActivity(operationName, ActivityKind.Client);
    }
}
