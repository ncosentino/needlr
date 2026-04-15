using System.Diagnostics;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Default implementation of <see cref="IIterativeAgentLoop"/> that runs an external loop
/// with fresh prompts per iteration, bypassing <c>FunctionInvokingChatClient</c>'s
/// accumulating conversation history.
/// </summary>
[DoNotAutoRegister]
internal sealed class IterativeAgentLoop : IIterativeAgentLoop
{
    private readonly IChatClientAccessor _chatClientAccessor;
    private readonly IAgentDiagnosticsWriter? _diagnosticsWriter;
    private readonly IAgentExecutionContextAccessor? _executionContextAccessor;

    internal IterativeAgentLoop(
        IChatClientAccessor chatClientAccessor,
        IAgentDiagnosticsWriter? diagnosticsWriter = null,
        IAgentExecutionContextAccessor? executionContextAccessor = null)
    {
        _chatClientAccessor = chatClientAccessor;
        _diagnosticsWriter = diagnosticsWriter;
        _executionContextAccessor = executionContextAccessor;
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
        var iterations = new List<IterationRecord>();
        string? finalResponse = null;
        var succeeded = true;
        string? errorMessage = null;

        var diagnosticsBuilder = AgentRunDiagnosticsBuilder.StartNew(options.LoopName);

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
                string userPrompt;
                try
                {
                    userPrompt = options.PromptFactory(context);
                }
                catch (Exception ex)
                {
                    succeeded = false;
                    errorMessage = $"Prompt factory failed on iteration {i}: {ex.Message}";
                    diagnosticsBuilder.RecordFailure(errorMessage);
                    break;
                }

                var iterationStopwatch = Stopwatch.StartNew();
                var iterationToolCalls = new List<ToolCallResult>();
                string? iterationResponseText = null;
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

                var chatOptions = new ChatOptions
                {
                    Tools = options.Tools.Cast<AITool>().ToList(),
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

                    var completionSequence = diagnosticsBuilder.NextChatCompletionSequence();
                    var completionStartedAt = DateTimeOffset.UtcNow;
                    var completionStopwatch = Stopwatch.StartNew();
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
                    catch (Exception ex)
                    {
                        completionStopwatch.Stop();
                        diagnosticsBuilder.AddChatCompletion(new ChatCompletionDiagnostics(
                            Sequence: completionSequence,
                            Model: "unknown",
                            Tokens: new TokenUsage(0, 0, 0, 0, 0),
                            InputMessageCount: messages.Count,
                            Duration: completionStopwatch.Elapsed,
                            Succeeded: false,
                            ErrorMessage: ex.Message,
                            StartedAt: completionStartedAt,
                            CompletedAt: DateTimeOffset.UtcNow));
                        diagnosticsBuilder.RecordInputMessageCount(messages.Count);
                        throw;
                    }

                    completionStopwatch.Stop();
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

                    // Record chat completion diagnostics
                    diagnosticsBuilder.AddChatCompletion(new ChatCompletionDiagnostics(
                        Sequence: completionSequence,
                        Model: response.ModelId ?? "unknown",
                        Tokens: new TokenUsage(
                            InputTokens: callInput,
                            OutputTokens: callOutput,
                            TotalTokens: callTotal,
                            CachedInputTokens: 0,
                            ReasoningTokens: 0),
                        InputMessageCount: messages.Count,
                        Duration: completionStopwatch.Elapsed,
                        Succeeded: true,
                        ErrorMessage: null,
                        StartedAt: completionStartedAt,
                        CompletedAt: DateTimeOffset.UtcNow));
                    diagnosticsBuilder.RecordInputMessageCount(messages.Count);
                    diagnosticsBuilder.RecordOutputMessageCount(responseMessageCount);

                    // Check for tool calls in response
                    var functionCalls = response.Messages
                        .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                        .ToList();

                    if (functionCalls.Count == 0)
                    {
                        // Model produced text — natural termination for this iteration
                        iterationResponseText = string.Join("",
                            response.Messages.SelectMany(m => m.Contents.OfType<TextContent>())
                                .Select(t => t.Text));
                        break;
                    }

                    // Execute tool calls
                    var roundResults = await ExecuteToolCallsAsync(
                        functionCalls, options.Tools, diagnosticsBuilder,
                        i, options.OnToolCall, cancellationToken)
                        .ConfigureAwait(false);
                    iterationToolCalls.AddRange(roundResults);

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
                    ResponseText: iterationResponseText,
                    Tokens: tokenUsage,
                    Duration: iterationStopwatch.Elapsed,
                    LlmCallCount: llmCallCount));

                // Update context for next iteration
                context.LastToolResults = iterationToolCalls;

                // Hook: iteration end (wrapped to escape catch-all)
                if (options.OnIterationEnd != null)
                {
                    await InvokeHookAsync(options.OnIterationEnd, iterations[^1]).ConfigureAwait(false);
                }

                // Check IsComplete predicate
                if (options.IsComplete?.Invoke(context) == true)
                {
                    break;
                }

                // If model produced text (no tool calls), the loop is done
                if (iterationResponseText != null)
                {
                    finalResponse = iterationResponseText;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            succeeded = false;
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
            errorMessage = ex.Message;
            diagnosticsBuilder.RecordFailure(errorMessage);
        }

        if (finalResponse == null && iterations.Count > 0)
        {
            // Get final response from last iteration if available
            finalResponse = iterations[^1].ResponseText;
        }

        var diagnostics = diagnosticsBuilder.Build();
        diagnosticsBuilder.Dispose();
        _diagnosticsWriter?.Set(diagnostics);
        executionContextScope?.Dispose();

        return new IterativeLoopResult(
            Iterations: iterations,
            FinalResponse: finalResponse,
            Diagnostics: diagnostics,
            Succeeded: succeeded,
            ErrorMessage: errorMessage);
    }

    private static async Task<List<ToolCallResult>> ExecuteToolCallsAsync(
        List<FunctionCallContent> functionCalls,
        IReadOnlyList<AITool> tools,
        AgentRunDiagnosticsBuilder diagnosticsBuilder,
        int iteration,
        Func<int, ToolCallResult, Task>? onToolCall,
        CancellationToken cancellationToken)
    {
        var toolMap = tools.OfType<AIFunction>()
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var results = new List<ToolCallResult>();

        foreach (var fc in functionCalls)
        {
            var sequence = diagnosticsBuilder.NextToolCallSequence();
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

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
                    CustomMetrics: null));

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
                    CustomMetrics: null));

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
