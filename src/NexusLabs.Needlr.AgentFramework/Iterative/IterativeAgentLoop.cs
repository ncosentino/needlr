using System.Diagnostics;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Default implementation of <see cref="IIterativeAgentLoop"/> that runs an external loop
/// with fresh prompts per iteration, bypassing <c>FunctionInvokingChatClient</c>'s
/// accumulating conversation history.
/// </summary>
/// <remarks>
/// <para>
/// Internally wraps the chat client with <see cref="DiagnosticsChatClientMiddleware"/>,
/// which is the single writer for <see cref="ChatCompletionDiagnostics"/>. The loop
/// itself writes <see cref="ToolCallDiagnostics"/> and OTel metrics for tool calls
/// and run lifecycle. Do not add external chat-completion recording middleware when
/// using this loop — it will produce duplicates.
/// </para>
/// </remarks>
[DoNotAutoRegister]
internal sealed class IterativeAgentLoop : IIterativeAgentLoop
{
    private readonly IChatClientAccessor _chatClientAccessor;
    private readonly IAgentDiagnosticsWriter? _diagnosticsWriter;
    private readonly IAgentExecutionContextAccessor? _executionContextAccessor;
    private readonly IProgressReporterAccessor? _progressReporterAccessor;
    private readonly ITokenBudgetTracker? _budgetTracker;
    private readonly IAgentMetrics? _metrics;

    internal IterativeAgentLoop(
        IChatClientAccessor chatClientAccessor,
        IAgentDiagnosticsWriter? diagnosticsWriter = null,
        IAgentExecutionContextAccessor? executionContextAccessor = null,
        IProgressReporterAccessor? progressReporterAccessor = null,
        ITokenBudgetTracker? budgetTracker = null,
        IAgentMetrics? metrics = null)
    {
        _chatClientAccessor = chatClientAccessor;
        _diagnosticsWriter = diagnosticsWriter;
        _executionContextAccessor = executionContextAccessor;
        _progressReporterAccessor = progressReporterAccessor;
        _budgetTracker = budgetTracker;
        _metrics = metrics;
    }

    /// <summary>
    /// Sentinel wrapper so lifecycle hook exceptions escape the framework catch-all.
    /// </summary>
    private sealed class LifecycleHookException(Exception inner) : Exception(inner.Message, inner);

    public async Task<IterativeLoopResult> RunAsync(
        IterativeLoopOptions options,
        IterativeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);

        var chatClient = _chatClientAccessor.ChatClient;

        // Apply per-loop middleware if configured
        if (options.ChatClientFactory is { } loopClientFactory)
        {
            chatClient = loopClientFactory(chatClient);
        }

        // Install diagnostics recording middleware only when the pipeline does
        // not already contain one. UsingDiagnostics(), a per-loop factory, or
        // manual wiring may have already installed a DiagnosticsRecordingChatClient.
        // Installing a second instance would cause every ChatCompletion to be
        // recorded twice, inflating token counts by 2×.
        //
        // Detection uses MEAI's GetService<T>() which walks the DelegatingChatClient
        // chain, so it works regardless of where the middleware was installed.
        if (chatClient.GetService<DiagnosticsRecordingChatClient>() is null)
        {
            var chatMiddleware = new DiagnosticsChatClientMiddleware(_metrics, _progressReporterAccessor);
            chatClient = new DiagnosticsRecordingChatClient(chatClient, chatMiddleware);
        }

        var iterations = new List<IterationRecord>();
        ChatResponse? finalResponse = null;
        var succeeded = true;
        string? errorMessage = null;
        var termination = TerminationReason.Completed;
        int totalToolCalls = 0;

        var diagnosticsBuilder = AgentRunDiagnosticsBuilder.StartNew(options.LoopName);
        diagnosticsBuilder.SetExecutionMode("IterativeLoop");
        _metrics?.RecordRunStarted(options.LoopName);

        // Bridge: if an execution context accessor is available, set up a scope
        // so that DI-resolved tools can access the workspace via
        // IAgentExecutionContextAccessor.Current.GetRequiredWorkspace().
        IDisposable? executionContextScope = null;
        if (_executionContextAccessor != null)
        {
            var executionContext = options.ExecutionContext
                ?? new AgentExecutionContext(
                    UserId: "iterative-loop",
                    OrchestrationId: options.LoopName,
                    Workspace: context.Workspace);
            executionContextScope = _executionContextAccessor.BeginScope(executionContext);
        }

        try
        {
            context.CancellationToken = cancellationToken;

            for (int i = 0; i < options.MaxIterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                context.Iteration = i;

                // Hook: iteration start (wrapped to escape catch-all)
                if (options.OnIterationStart != null)
                {
                    await InvokeHookAsync(options.OnIterationStart, i, context).ConfigureAwait(false);
                }

                // Build fresh prompt from workspace state
                var budgetPressureTriggered = false;
                string userPrompt;
                try
                {
                    userPrompt = options.PromptFactory(context);
                }
                catch (Exception ex)
                {
                    succeeded = false;
                    termination = TerminationReason.Error;
                    errorMessage = $"Prompt factory failed on iteration {i}: {ex.Message}";
                    diagnosticsBuilder.RecordFailure(errorMessage);
                    break;
                }

                // Budget pressure: if token usage is at or above the threshold,
                // prepend the finalization instruction and mark this as the last iteration.
                if (options.BudgetPressureThreshold is { } threshold
                    && _budgetTracker is { MaxTokens: > 0 } tracker)
                {
                    var usage = (double)tracker.CurrentTokens / tracker.MaxTokens.Value;
                    if (usage >= threshold)
                    {
                        userPrompt = options.BudgetPressureInstruction + "\n\n" + userPrompt;
                        budgetPressureTriggered = true;
                    }
                }

                var iterationStopwatch = Stopwatch.StartNew();
                var iterationToolCalls = new List<ToolCallResult>();
                ChatResponse? iterationResponse = null;
                long iterationInputTokens = 0;
                long iterationOutputTokens = 0;
                long iterationTotalTokens = 0;
                int llmCallCount = 0;

                // Build messages — always just [system, user], no history
                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, options.Instructions),
                    new(ChatRole.User, userPrompt),
                };

                var effectiveTools = options.ToolFilter is { } filter
                    ? filter(i, context, options.Tools)
                    : options.Tools;

                var chatOptions = new ChatOptions
                {
                    Tools = effectiveTools.Cast<AITool>().ToList(),
                };

                // Execute rounds within this iteration based on ToolResultMode
                var maxRounds = options.ToolResultMode switch
                {
                    ToolResultMode.SingleCall => 1,
                    ToolResultMode.OneRoundTrip => 2,
                    ToolResultMode.MultiRound => options.MaxToolRoundsPerIteration,
                    _ => 1,
                };

                for (int round = 0; round < maxRounds; round++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check budget pressure between rounds (not just per iteration)
                    if (round > 0
                        && !budgetPressureTriggered
                        && options.BudgetPressureThreshold is { } roundThreshold
                        && _budgetTracker is { MaxTokens: > 0 } roundTracker)
                    {
                        var roundUsage = (double)roundTracker.CurrentTokens / roundTracker.MaxTokens.Value;
                        if (roundUsage >= roundThreshold)
                        {
                            budgetPressureTriggered = true;
                            break;
                        }
                    }

                    ChatResponse response;

                    try
                    {
                        response = await chatClient.GetResponseAsync(
                            messages, chatOptions, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // let outer handler deal with cancellation
                    }
                    catch (Exception)
                    {
                        // Chat completion diagnostics are recorded by the middleware
                        // wrapping the chat client — the loop does not record them.
                        diagnosticsBuilder.RecordInputMessageCount(messages.Count);
                        throw;
                    }

                    llmCallCount++;

                    // Track tokens
                    long callInput = 0, callOutput = 0, callTotal = 0;
                    if (response.Usage is { } usage)
                    {
                        callInput = usage.InputTokenCount ?? 0;
                        callOutput = usage.OutputTokenCount ?? 0;
                        callTotal = usage.TotalTokenCount ?? 0;
                        iterationInputTokens += callInput;
                        iterationOutputTokens += callOutput;
                        iterationTotalTokens += callTotal;
                    }

                    var responseMessageCount = response.Messages.Count;

                    // Chat completion diagnostics are recorded by the middleware
                    // wrapping the chat client — the loop does not record them.
                    diagnosticsBuilder.RecordInputMessageCount(messages.Count);
                    diagnosticsBuilder.RecordOutputMessageCount(responseMessageCount);

                    // Check for tool calls in response
                    var functionCalls = response.Messages
                        .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                        .ToList();

                    if (functionCalls.Count == 0)
                    {
                        // Model produced text — natural termination for this iteration.
                        // Capture the full ChatResponse to preserve messages, usage, and
                        // any other metadata for downstream consumers and evaluation.
                        iterationResponse = response;
                        break;
                    }

                    // Execute tool calls — limit to remaining allowance if MaxTotalToolCalls is set
                    var remainingAllowance = options.MaxTotalToolCalls.HasValue
                        ? options.MaxTotalToolCalls.Value - totalToolCalls
                        : (int?)null;

                    var callsToExecute = remainingAllowance.HasValue && remainingAllowance.Value < functionCalls.Count
                        ? functionCalls.Take(remainingAllowance.Value).ToList()
                        : functionCalls;

                    var roundResults = await ExecuteToolCallsAsync(
                        callsToExecute, options.Tools, diagnosticsBuilder,
                        i, options.OnToolCall, _progressReporterAccessor,
                        _metrics, cancellationToken)
                        .ConfigureAwait(false);
                    iterationToolCalls.AddRange(roundResults);
                    totalToolCalls += roundResults.Count;

                    // Check MaxTotalToolCalls guard
                    if (options.MaxTotalToolCalls is { } maxCalls && totalToolCalls >= maxCalls)
                    {
                        termination = TerminationReason.MaxToolCallsReached;
                        succeeded = false;
                        errorMessage = $"Cumulative tool call count ({totalToolCalls}) reached MaxTotalToolCalls ({maxCalls}).";
                        diagnosticsBuilder.RecordFailure(errorMessage);
                        break;
                    }

                    // For SingleCall mode, don't send results back — just store them
                    if (options.ToolResultMode == ToolResultMode.SingleCall)
                    {
                        break;
                    }

                    // For OneRoundTrip/MultiRound, send results back to model
                    // Add assistant message with tool calls
                    var assistantMessage = new ChatMessage(ChatRole.Assistant,
                        functionCalls.Select(fc => (AIContent)fc).ToList());
                    messages.Add(assistantMessage);

                    // Add tool result messages
                    foreach (var (fc, result) in functionCalls.Zip(roundResults))
                    {
                        var resultContent = result.Succeeded
                            ? result.Result?.ToString() ?? ""
                            : $"Error: {result.ErrorMessage}";

                        messages.Add(new ChatMessage(ChatRole.Tool,
                            [new FunctionResultContent(fc.CallId, resultContent)]));
                    }

                    // For OneRoundTrip, if this was the first round (round 0),
                    // we'll do ONE more LLM call. If it's round 1, we're done.
                    // For MultiRound, we continue until maxRounds or text response.
                }

                // If a guard triggered termination inside the round loop, break outer loop too
                if (termination == TerminationReason.MaxToolCallsReached)
                {
                    // Still record the partial iteration
                    iterationStopwatch.Stop();
                    iterations.Add(new IterationRecord(
                        Iteration: i,
                        ToolCalls: iterationToolCalls,
                        FinalResponse: iterationResponse,
                        Tokens: new TokenUsage(iterationInputTokens, iterationOutputTokens, iterationTotalTokens, 0, 0),
                        Duration: iterationStopwatch.Elapsed,
                        LlmCallCount: llmCallCount,
                        ToolCallCount: iterationToolCalls.Count));
                    context.LastToolResults = iterationToolCalls;
                    break;
                }

                iterationStopwatch.Stop();

                var tokenUsage = new TokenUsage(
                    InputTokens: iterationInputTokens,
                    OutputTokens: iterationOutputTokens,
                    TotalTokens: iterationTotalTokens,
                    CachedInputTokens: 0,
                    ReasoningTokens: 0);

                iterations.Add(new IterationRecord(
                    Iteration: i,
                    ToolCalls: iterationToolCalls,
                    FinalResponse: iterationResponse,
                    Tokens: tokenUsage,
                    Duration: iterationStopwatch.Elapsed,
                    LlmCallCount: llmCallCount,
                    ToolCallCount: iterationToolCalls.Count));

                // Update context for next iteration
                context.LastToolResults = iterationToolCalls;

                // Hook: iteration end (wrapped to escape catch-all)
                if (options.OnIterationEnd != null)
                {
                    await InvokeHookAsync(options.OnIterationEnd, iterations[^1]).ConfigureAwait(false);
                }

                // Stall detection — compare consecutive iterations
                if (options.StallDetection is { } stallOpts && iterations.Count >= 2)
                {
                    var currentTokens = iterations[^1].Tokens.TotalTokens;
                    var consecutiveSimilar = 0;

                    for (int s = iterations.Count - 2; s >= 0; s--)
                    {
                        var prevTokens = iterations[s].Tokens.TotalTokens;
                        if (prevTokens > 0)
                        {
                            var delta = Math.Abs(currentTokens - prevTokens) / (double)prevTokens;
                            if (delta <= stallOpts.TolerancePercent)
                            {
                                consecutiveSimilar++;
                                currentTokens = prevTokens;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (consecutiveSimilar >= stallOpts.ConsecutiveThreshold - 1)
                    {
                        termination = TerminationReason.StallDetected;
                        succeeded = false;
                        errorMessage = $"Stall detected: {consecutiveSimilar + 1} consecutive iterations " +
                            $"with similar token counts (~{iterations[^1].Tokens.TotalTokens} tokens, " +
                            $"tolerance {stallOpts.TolerancePercent:P0}).";
                        diagnosticsBuilder.RecordFailure(errorMessage);
                        break;
                    }
                }

                // Check IsComplete predicate
                if (options.IsComplete?.Invoke(context) == true)
                {
                    termination = TerminationReason.Completed;
                    break;
                }

                // Budget pressure: this was the finalization iteration — stop now
                if (budgetPressureTriggered)
                {
                    termination = TerminationReason.BudgetPressure;
                    break;
                }

                // If model produced text (no tool calls), the loop is done
                if (iterationResponse != null)
                {
                    finalResponse = iterationResponse;
                    termination = TerminationReason.NaturalCompletion;
                    break;
                }
            }

            // If the loop exhausted MaxIterations without IsComplete returning true
            // and without a text response, that's a failure — the agent didn't finish.
            if (termination == TerminationReason.Completed
                && finalResponse == null
                && options.IsComplete?.Invoke(context) != true
                && iterations.Count >= options.MaxIterations)
            {
                succeeded = false;
                termination = TerminationReason.MaxIterationsReached;
                errorMessage = $"Loop exhausted {options.MaxIterations} iterations without completing. "
                    + "The IsComplete predicate never returned true and the model never produced a text response.";
                diagnosticsBuilder.RecordFailure(errorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            succeeded = false;
            termination = TerminationReason.Cancelled;
            errorMessage = "Loop was cancelled.";
            diagnosticsBuilder.RecordFailure(errorMessage);
        }
        catch (LifecycleHookException hookEx)
        {
            // Lifecycle hook exceptions propagate to the caller — they are
            // user-controlled code and should not be silently swallowed.
            throw hookEx.InnerException!;
        }
        catch (Exception ex)
        {
            succeeded = false;
            termination = TerminationReason.Error;
            errorMessage = ex.Message;
            diagnosticsBuilder.RecordFailure(errorMessage);
        }

        if (finalResponse == null && iterations.Count > 0)
        {
            // Get final response from last iteration if available
            finalResponse = iterations[^1].FinalResponse;
        }

        var diagnostics = diagnosticsBuilder.Build();
        diagnosticsBuilder.Dispose();
        _diagnosticsWriter?.Set(diagnostics);
        _metrics?.RecordRunCompleted(diagnostics);
        executionContextScope?.Dispose();

        var configuration = new IterativeLoopConfiguration(
            ToolResultMode: options.ToolResultMode,
            MaxIterations: options.MaxIterations,
            MaxToolRoundsPerIteration: options.MaxToolRoundsPerIteration,
            MaxTotalToolCalls: options.MaxTotalToolCalls,
            BudgetPressureThreshold: options.BudgetPressureThreshold,
            LoopName: options.LoopName);

        return new IterativeLoopResult(
            Iterations: iterations,
            FinalResponse: finalResponse,
            Diagnostics: diagnostics,
            Succeeded: succeeded,
            ErrorMessage: errorMessage,
            Termination: termination,
            Configuration: configuration);
    }

    private static async Task<List<ToolCallResult>> ExecuteToolCallsAsync(
        List<FunctionCallContent> functionCalls,
        IReadOnlyList<AITool> tools,
        AgentRunDiagnosticsBuilder diagnosticsBuilder,
        int iteration,
        Func<int, ToolCallResult, Task>? onToolCall,
        IProgressReporterAccessor? progressAccessor,
        IAgentMetrics? metrics,
        CancellationToken cancellationToken)
    {
        var toolMap = tools.OfType<AIFunction>()
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var results = new List<ToolCallResult>();
        var reporter = progressAccessor?.Current;

        foreach (var fc in functionCalls)
        {
            var sequence = diagnosticsBuilder.NextToolCallSequence();
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            using var activity = metrics?.ActivitySource.StartActivity($"agent.tool {fc.Name}", ActivityKind.Internal);
            activity?.SetTag("agent.tool.name", fc.Name);
            activity?.SetTag("agent.tool.sequence", sequence);
            activity?.SetTag("gen_ai.agent.name", diagnosticsBuilder.AgentName);

            reporter?.Report(new ToolCallStartedEvent(
                Timestamp: startedAt,
                WorkflowId: reporter.WorkflowId,
                AgentId: reporter.AgentId,
                ParentAgentId: null,
                Depth: reporter.Depth,
                SequenceNumber: reporter.NextSequence(),
                ToolName: fc.Name));

            if (!toolMap.TryGetValue(fc.Name, out var function))
            {
                stopwatch.Stop();
                var errorResult = new ToolCallResult(
                    FunctionName: fc.Name,
                    Arguments: ToReadOnly(fc.Arguments),
                    Result: null,
                    Duration: stopwatch.Elapsed,
                    Succeeded: false,
                    ErrorMessage: $"Unknown tool: '{fc.Name}'");

                diagnosticsBuilder.AddToolCall(new ToolCallDiagnostics(
                    Sequence: sequence,
                    ToolName: fc.Name,
                    Duration: stopwatch.Elapsed,
                    Succeeded: false,
                    ErrorMessage: errorResult.ErrorMessage,
                    StartedAt: startedAt,
                    CompletedAt: DateTimeOffset.UtcNow,
                    CustomMetrics: null)
                {
                    AgentName = diagnosticsBuilder.AgentName,
                    Arguments = ToReadOnly(fc.Arguments),
                    ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(fc.Arguments),
                });
                metrics?.RecordToolCall(fc.Name, stopwatch.Elapsed, succeeded: false, agentName: diagnosticsBuilder.AgentName);
                activity?.SetStatus(ActivityStatusCode.Error, errorResult.ErrorMessage);
                activity?.SetTag("status", "failed");

                reporter?.Report(new ToolCallFailedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: reporter.WorkflowId,
                    AgentId: reporter.AgentId,
                    ParentAgentId: null,
                    Depth: reporter.Depth,
                    SequenceNumber: reporter.NextSequence(),
                    ToolName: fc.Name,
                    ErrorMessage: errorResult.ErrorMessage ?? "Unknown tool",
                    Duration: stopwatch.Elapsed));

                results.Add(errorResult);

                if (onToolCall != null)
                {
                    await InvokeHookAsync(onToolCall, iteration, errorResult).ConfigureAwait(false);
                }

                continue;
            }

            try
            {
                var result = await function.InvokeAsync(
                    fc.Arguments is { } args ? new AIFunctionArguments(args) : null,
                    cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();

                diagnosticsBuilder.AddToolCall(new ToolCallDiagnostics(
                    Sequence: sequence,
                    ToolName: fc.Name,
                    Duration: stopwatch.Elapsed,
                    Succeeded: true,
                    ErrorMessage: null,
                    StartedAt: startedAt,
                    CompletedAt: DateTimeOffset.UtcNow,
                    CustomMetrics: null)
                {
                    AgentName = diagnosticsBuilder.AgentName,
                    Arguments = ToReadOnly(fc.Arguments),
                    Result = result,
                    ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(fc.Arguments),
                    ResultCharCount = DiagnosticsCharCounter.JsonLength(result),
                });
                metrics?.RecordToolCall(fc.Name, stopwatch.Elapsed, succeeded: true, agentName: diagnosticsBuilder.AgentName);
                activity?.SetTag("status", "success");

                reporter?.Report(new ToolCallCompletedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: reporter.WorkflowId,
                    AgentId: reporter.AgentId,
                    ParentAgentId: null,
                    Depth: reporter.Depth,
                    SequenceNumber: reporter.NextSequence(),
                    ToolName: fc.Name,
                    Duration: stopwatch.Elapsed,
                    CustomMetrics: null));

                results.Add(new ToolCallResult(
                    FunctionName: fc.Name,
                    Arguments: ToReadOnly(fc.Arguments),
                    Result: result,
                    Duration: stopwatch.Elapsed,
                    Succeeded: true,
                    ErrorMessage: null));

                if (onToolCall != null)
                {
                    await InvokeHookAsync(onToolCall, iteration, results[^1]).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                diagnosticsBuilder.AddToolCall(new ToolCallDiagnostics(
                    Sequence: sequence,
                    ToolName: fc.Name,
                    Duration: stopwatch.Elapsed,
                    Succeeded: false,
                    ErrorMessage: ex.Message,
                    StartedAt: startedAt,
                    CompletedAt: DateTimeOffset.UtcNow,
                    CustomMetrics: null)
                {
                    AgentName = diagnosticsBuilder.AgentName,
                    Arguments = ToReadOnly(fc.Arguments),
                    ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(fc.Arguments),
                });
                metrics?.RecordToolCall(fc.Name, stopwatch.Elapsed, succeeded: false, agentName: diagnosticsBuilder.AgentName);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("status", "failed");

                reporter?.Report(new ToolCallFailedEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    WorkflowId: reporter.WorkflowId,
                    AgentId: reporter.AgentId,
                    ParentAgentId: null,
                    Depth: reporter.Depth,
                    SequenceNumber: reporter.NextSequence(),
                    ToolName: fc.Name,
                    ErrorMessage: ex.Message,
                    Duration: stopwatch.Elapsed));

                results.Add(new ToolCallResult(
                    FunctionName: fc.Name,
                    Arguments: ToReadOnly(fc.Arguments),
                    Result: null,
                    Duration: stopwatch.Elapsed,
                    Succeeded: false,
                    ErrorMessage: ex.Message));

                if (onToolCall != null)
                {
                    await InvokeHookAsync(onToolCall, iteration, results[^1]).ConfigureAwait(false);
                }
            }
        }

        return results;
    }

    private static async Task InvokeHookAsync<T>(Func<T, Task> hook, T arg)
    {
        try
        {
            await hook(arg).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new LifecycleHookException(ex);
        }
    }

    private static async Task InvokeHookAsync<T1, T2>(Func<T1, T2, Task> hook, T1 arg1, T2 arg2)
    {
        try
        {
            await hook(arg1, arg2).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new LifecycleHookException(ex);
        }
    }

    private static IReadOnlyDictionary<string, object?> ToReadOnly(
        IDictionary<string, object?>? arguments) =>
        arguments is IReadOnlyDictionary<string, object?> ro
            ? ro
            : arguments is not null
                ? new Dictionary<string, object?>(arguments)
                : new Dictionary<string, object?>();
}
