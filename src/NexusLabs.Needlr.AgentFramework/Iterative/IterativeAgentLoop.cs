using System.Diagnostics;

using Microsoft.Extensions.AI;

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

    internal IterativeAgentLoop(IChatClientAccessor chatClientAccessor)
    {
        _chatClientAccessor = chatClientAccessor;
    }

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

        try
        {
            context.CancellationToken = cancellationToken;

            for (int i = 0; i < options.MaxIterations; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                context.Iteration = i;

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

                    var response = await chatClient.GetResponseAsync(
                        messages, chatOptions, cancellationToken).ConfigureAwait(false);

                    llmCallCount++;

                    // Track tokens
                    if (response.Usage is { } usage)
                    {
                        iterationInputTokens += usage.InputTokenCount ?? 0;
                        iterationOutputTokens += usage.OutputTokenCount ?? 0;
                        iterationTotalTokens += usage.TotalTokenCount ?? 0;
                    }

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
                        functionCalls, options.Tools, diagnosticsBuilder, cancellationToken)
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
            }
        }

        return results;
    }

    private static IReadOnlyDictionary<string, object?> ToReadOnly(
        IDictionary<string, object?>? arguments) =>
        arguments is IReadOnlyDictionary<string, object?> ro
            ? ro
            : arguments is not null
                ? new Dictionary<string, object?>(arguments)
                : new Dictionary<string, object?>();
}
