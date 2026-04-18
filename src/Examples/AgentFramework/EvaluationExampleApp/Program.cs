using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.Copilot;

// =============================================================================
// EvaluationExampleApp
// -----------------------------------------------------------------------------
// Demonstrates that Needlr's Phase-1 retyped live-path surfaces plug into
// Microsoft.Extensions.AI.Evaluation without any flattening or adapter code.
//
//   1. Build an IterativeLoopResult with the new FinalResponse : ChatResponse?
//      and a single IterationRecord carrying ToolCallResults.
//   2. Feed IterativeLoopResult.FinalResponse straight into RelevanceEvaluator.
//   3. Convert IterationRecord tool calls to an MEAI trajectory via the
//      ToToolCallTrajectory() extension and print the resulting shape.
//
// The judge is a real CopilotChatClient — no mocks. Requires an authenticated
// GitHub Copilot CLI (run `gh auth login` first). No API keys needed.
// =============================================================================

PrintHeader("Needlr + Microsoft.Extensions.AI.Evaluation");

var loopResult = BuildSampleLoopResult();
var iteration = loopResult.Iterations[0];

PrintSection("Sample IterativeLoopResult (native MEAI shapes)");
Console.WriteLine($"  Succeeded          : {loopResult.Succeeded}");
Console.WriteLine($"  Termination        : {loopResult.Termination}");
Console.WriteLine($"  Iterations         : {loopResult.Iterations.Count}");
Console.WriteLine($"  FinalResponse.Text : {loopResult.FinalResponse?.Text}");
Console.WriteLine($"  FinalResponse type : {loopResult.FinalResponse?.GetType().Name}");

PrintSection("Tool-call trajectory (ToToolCallTrajectory extension)");
var trajectory = iteration.ToToolCallTrajectory();
foreach (var message in trajectory)
{
    var contentSummary = string.Join(
        ", ",
        message.Contents.Select(c => c.GetType().Name));
    Console.WriteLine($"  [{message.Role}] {contentSummary}");
}

var judge = new CopilotChatClient(new CopilotChatClientOptions
{
    DefaultModel = "claude-sonnet-4",
});
Console.WriteLine("  Judge: CopilotChatClient (model: claude-sonnet-4)");
var chatConfiguration = new ChatConfiguration(judge);

PrintSection("Demo A — RelevanceEvaluator on IterativeLoopResult.FinalResponse");
await TryRunEvaluatorAsync(
    "RelevanceEvaluator",
    async () =>
    {
        var evaluator = new RelevanceEvaluator();
        var userPrompt = new ChatMessage(
            ChatRole.User,
            "Summarize the weather in Seattle and Portland.");
        var response = loopResult.FinalResponse
            ?? new ChatResponse([new ChatMessage(ChatRole.Assistant, string.Empty)]);
        return await evaluator.EvaluateAsync(
            [userPrompt],
            response,
            chatConfiguration);
    });

PrintSection("Demo B — ToolCallAccuracyEvaluator on trajectory");
await TryRunEvaluatorAsync(
    "ToolCallAccuracyEvaluator",
    async () =>
    {
        // ToolCallAccuracyEvaluator is flagged [Experimental("AIEVAL001")] in
        // Microsoft.Extensions.AI.Evaluation.Quality. Suppress only at the
        // instantiation + context-construction site so the rest of the sample
        // stays warning-clean.
#pragma warning disable AIEVAL001
        var evaluator = new ToolCallAccuracyEvaluator();

        // The evaluator inspects FunctionCallContent in modelResponse and
        // cross-references it against the supplied tool definitions.
        var getWeatherTool = AIFunctionFactory.Create(
            (string city) => $"Weather for {city}",
            name: "get_weather",
            description: "Gets the current weather for a given city.");

        var toolContext = new ToolCallAccuracyEvaluatorContext(getWeatherTool);
#pragma warning restore AIEVAL001

        var userPrompt = new ChatMessage(
            ChatRole.User,
            "Summarize the weather in Seattle and Portland.");

        // Collapse all FunctionCallContents from the trajectory into a single
        // assistant ChatResponse — this is the "model response being judged".
        var functionCalls = trajectory
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Cast<AIContent>()
            .ToList();
        var assistantMessage = new ChatMessage(ChatRole.Assistant, functionCalls);
        var response = new ChatResponse([assistantMessage])
        {
            ModelId = "mock-model",
        };

        return await evaluator.EvaluateAsync(
            [userPrompt],
            response,
            chatConfiguration,
            [toolContext]);
    });

PrintSection("Demo C — Character counts on captured diagnostics (Phase 2.5c)");
PrintCharCountsDemo();

PrintSection("Demo D — Ordered diagnostics timeline (Phase 2.5d)");
PrintOrderedTimelineDemo();

PrintSection("Demo E — Transcript markdown rendering (Phase 2.5f)");
PrintTranscriptMarkdownDemo();

PrintHeader("Done");
return 0;

static void PrintCharCountsDemo()
{
    var requestMessages = new[]
    {
        new ChatMessage(ChatRole.System, "You are a helpful weather assistant."),
        new ChatMessage(ChatRole.User, "Summarize the weather in Seattle and Portland."),
    };
    var responseMessage = new ChatMessage(
        ChatRole.Assistant,
        "Seattle is 52F with light rain; Portland is 55F and cloudy.");
    var chatResponse = new ChatResponse([responseMessage]) { ModelId = "mock-model" };

    var chatDiag = new ChatCompletionDiagnostics(
        Sequence: 0,
        Model: "mock-model",
        Tokens: new TokenUsage(
            InputTokens: 240,
            OutputTokens: 60,
            TotalTokens: 300,
            CachedInputTokens: 0,
            ReasoningTokens: 0),
        InputMessageCount: requestMessages.Length,
        Duration: TimeSpan.FromMilliseconds(450),
        Succeeded: true,
        ErrorMessage: null,
        StartedAt: DateTimeOffset.UtcNow,
        CompletedAt: DateTimeOffset.UtcNow)
    {
        RequestMessages = requestMessages,
        Response = chatResponse,
        RequestCharCount = DiagnosticsCharCounter.ChatMessagesLength(requestMessages),
        ResponseCharCount = DiagnosticsCharCounter.ChatResponseLength(chatResponse),
    };

    Console.WriteLine(
        $"  ChatCompletion  : in={chatDiag.Tokens.InputTokens}tok / {chatDiag.RequestCharCount}chars, " +
        $"out={chatDiag.Tokens.OutputTokens}tok / {chatDiag.ResponseCharCount}chars");

    var toolArgs = new Dictionary<string, object?> { ["city"] = "Seattle" };
    var toolResult = "Seattle: 52F, light rain";
    var toolDiag = new ToolCallDiagnostics(
        Sequence: 1,
        ToolName: "get_weather",
        Duration: TimeSpan.FromMilliseconds(120),
        Succeeded: true,
        ErrorMessage: null,
        StartedAt: DateTimeOffset.UtcNow,
        CompletedAt: DateTimeOffset.UtcNow,
        CustomMetrics: null)
    {
        Arguments = toolArgs,
        Result = toolResult,
        ArgumentsCharCount = DiagnosticsCharCounter.JsonLength(toolArgs),
        ResultCharCount = DiagnosticsCharCounter.JsonLength(toolResult),
    };

    Console.WriteLine(
        $"  ToolCall        : {toolDiag.ToolName} " +
        $"args={toolDiag.ArgumentsCharCount}chars, result={toolDiag.ResultCharCount}chars");
    Console.WriteLine(
        "  (Character counts are programmatic truth — computed from serialized payloads, not LLM-reported tokens.)");
}

static void PrintOrderedTimelineDemo()
{
    var t0 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    var tokens = new TokenUsage(10, 20, 30, 0, 0);

    // Interleave: chat -> tool -> chat (LLM -> function call -> LLM observes result).
    var chat0 = new ChatCompletionDiagnostics(
        Sequence: 0, Model: "mock-model", Tokens: tokens,
        InputMessageCount: 1, Duration: TimeSpan.FromMilliseconds(100),
        Succeeded: true, ErrorMessage: null,
        StartedAt: t0, CompletedAt: t0.AddMilliseconds(100));

    var tool0 = new ToolCallDiagnostics(
        Sequence: 0, ToolName: "get_weather", Duration: TimeSpan.FromMilliseconds(50),
        Succeeded: true, ErrorMessage: null,
        StartedAt: t0.AddMilliseconds(110), CompletedAt: t0.AddMilliseconds(160),
        CustomMetrics: null);

    var chat1 = new ChatCompletionDiagnostics(
        Sequence: 1, Model: "mock-model", Tokens: tokens,
        InputMessageCount: 2, Duration: TimeSpan.FromMilliseconds(80),
        Succeeded: true, ErrorMessage: null,
        StartedAt: t0.AddMilliseconds(170), CompletedAt: t0.AddMilliseconds(250));

    // ChatCompletions and ToolCalls each start Sequence at 0 — so sequence alone
    // cannot order across kinds. GetOrderedTimeline sorts by wall-clock StartedAt.
    IAgentRunDiagnostics diag = new TimelineDemoDiagnostics(
        chats: [chat0, chat1],
        tools: [tool0]);

    var timeline = diag.GetOrderedTimeline();
    Console.WriteLine($"  Timeline entries: {timeline.Count}");
    foreach (var entry in timeline)
    {
        var offsetMs = (entry.StartedAt - t0).TotalMilliseconds;
        var detail = entry.Kind switch
        {
            DiagnosticsTimelineEntryKind.ChatCompletion =>
                $"model={entry.ChatCompletion!.Model}",
            DiagnosticsTimelineEntryKind.ToolCall =>
                $"tool={entry.ToolCall!.ToolName}",
            _ => "?",
        };
        Console.WriteLine(
            $"  [+{offsetMs,4:0}ms] {entry.Kind,-15} seq={entry.Sequence}  {detail}");
    }
    Console.WriteLine(
        "  (Ordered by StartedAt — ties break chat-before-tool, then by Sequence within kind.)");
}

static void PrintTranscriptMarkdownDemo()
{
    var t0 = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    var tokens = new TokenUsage(12, 24, 36, 0, 0);

    var chat0 = new ChatCompletionDiagnostics(
        Sequence: 0, Model: "mock-model", Tokens: tokens,
        InputMessageCount: 2, Duration: TimeSpan.FromMilliseconds(90),
        Succeeded: true, ErrorMessage: null,
        StartedAt: t0, CompletedAt: t0.AddMilliseconds(90));

    var tool0 = new ToolCallDiagnostics(
        Sequence: 0, ToolName: "get_weather", Duration: TimeSpan.FromMilliseconds(40),
        Succeeded: true, ErrorMessage: null,
        StartedAt: t0.AddMilliseconds(100), CompletedAt: t0.AddMilliseconds(140),
        CustomMetrics: null);

    var inputMessages = new List<ChatMessage>
    {
        new(ChatRole.System, "You are a helpful weather assistant."),
        new(ChatRole.User, "What's the weather in Seattle?"),
    };

    var outputResponse = new AgentResponse(new List<ChatMessage>
    {
        new(ChatRole.Assistant, "Seattle is 52F with light rain."),
    });

    IAgentRunDiagnostics diag = new TranscriptDemoDiagnostics(
        chats: [chat0],
        tools: [tool0],
        input: inputMessages,
        output: outputResponse);

    var markdown = diag.ToTranscriptMarkdown();
    Console.WriteLine(markdown);
    Console.WriteLine(
        "  (Deterministic: InvariantCulture, ordered timeline, JSON with WriteIndented=true.)");
}

static IterativeLoopResult BuildSampleLoopResult()
{
    var toolCalls = new[]
    {
        new ToolCallResult(
            FunctionName: "get_weather",
            Arguments: new Dictionary<string, object?> { ["city"] = "Seattle" },
            Result: "Seattle: 52F, light rain",
            Duration: TimeSpan.FromMilliseconds(120),
            Succeeded: true,
            ErrorMessage: null),
        new ToolCallResult(
            FunctionName: "get_weather",
            Arguments: new Dictionary<string, object?> { ["city"] = "Portland" },
            Result: "Portland: 55F, cloudy",
            Duration: TimeSpan.FromMilliseconds(118),
            Succeeded: true,
            ErrorMessage: null),
    };

    var finalResponse = new ChatResponse(
    [
        new ChatMessage(
            ChatRole.Assistant,
            "Seattle is 52F with light rain; Portland is 55F and cloudy."),
    ])
    {
        ModelId = "mock-model",
    };

    var tokens = new TokenUsage(
        InputTokens: 240,
        OutputTokens: 60,
        TotalTokens: 300,
        CachedInputTokens: 0,
        ReasoningTokens: 0);

    var record = new IterationRecord(
        Iteration: 1,
        ToolCalls: toolCalls,
        FinalResponse: finalResponse,
        Tokens: tokens,
        Duration: TimeSpan.FromMilliseconds(450),
        LlmCallCount: 2,
        ToolCallCount: toolCalls.Length);

    var configuration = new IterativeLoopConfiguration(
        ToolResultMode: ToolResultMode.OneRoundTrip,
        MaxIterations: 5,
        MaxToolRoundsPerIteration: 3,
        MaxTotalToolCalls: null,
        BudgetPressureThreshold: null,
        LoopName: "EvaluationDemo");

    return new IterativeLoopResult(
        Iterations: new[] { record },
        FinalResponse: finalResponse,
        Diagnostics: null,
        Succeeded: true,
        ErrorMessage: null,
        Termination: TerminationReason.Completed,
        Configuration: configuration);
}

static async Task TryRunEvaluatorAsync(
    string evaluatorName,
    Func<Task<EvaluationResult>> invoke)
{
    try
    {
        var result = await invoke();
        WriteColored(ConsoleColor.Green, $"  [OK] {evaluatorName} produced {result.Metrics.Count} metric(s)");
        foreach (var kvp in result.Metrics)
        {
            var metric = kvp.Value;
            Console.WriteLine($"    - {metric.Name} (interpretation: {metric.Interpretation?.Rating.ToString() ?? "n/a"})");
        }
    }
    catch (Exception ex)
    {
        WriteColored(
            ConsoleColor.Yellow,
            $"  [SKIPPED] {evaluatorName}: {ex.GetType().Name}: {ex.Message}");
        WriteColored(
            ConsoleColor.DarkYellow,
            "    (Deterministic trajectory + ChatResponse shapes above still demonstrate wiring.)");
    }
}

static void PrintHeader(string text)
{
    Console.WriteLine();
    WriteColored(ConsoleColor.Cyan, new string('=', 78));
    WriteColored(ConsoleColor.Cyan, $" {text}");
    WriteColored(ConsoleColor.Cyan, new string('=', 78));
}

static void PrintSection(string text)
{
    Console.WriteLine();
    WriteColored(ConsoleColor.White, $"-- {text} " + new string('-', Math.Max(0, 74 - text.Length)));
}

static void WriteColored(ConsoleColor color, string text)
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = previous;
}

// =============================================================================
// Stub for the ordered-timeline demo. AgentRunDiagnostics is internal, so the
// example implements IAgentRunDiagnostics directly with hand-built chat + tool
// entries interleaved in time.
// =============================================================================

internal sealed class TimelineDemoDiagnostics(
    IReadOnlyList<ChatCompletionDiagnostics> chats,
    IReadOnlyList<ToolCallDiagnostics> tools) : IAgentRunDiagnostics
{
    public string AgentName => "timeline-demo";
    public TimeSpan TotalDuration => TimeSpan.FromMilliseconds(250);
    public TokenUsage AggregateTokenUsage => new(0, 0, 0, 0, 0);
    public IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions => chats;
    public IReadOnlyList<ToolCallDiagnostics> ToolCalls => tools;
    public int TotalInputMessages => 0;
    public int TotalOutputMessages => 0;
    public IReadOnlyList<ChatMessage> InputMessages => [];
    public AgentResponse? OutputResponse => null;
    public bool Succeeded => true;
    public string? ErrorMessage => null;
    public DateTimeOffset StartedAt => DateTimeOffset.UnixEpoch;
    public DateTimeOffset CompletedAt => DateTimeOffset.UnixEpoch;
    public string? ExecutionMode => "demo";
}

internal sealed class TranscriptDemoDiagnostics(
    IReadOnlyList<ChatCompletionDiagnostics> chats,
    IReadOnlyList<ToolCallDiagnostics> tools,
    IReadOnlyList<ChatMessage> input,
    AgentResponse output) : IAgentRunDiagnostics
{
    public string AgentName => "transcript-demo";
    public TimeSpan TotalDuration => TimeSpan.FromMilliseconds(150);
    public TokenUsage AggregateTokenUsage => new(12, 24, 36, 0, 0);
    public IReadOnlyList<ChatCompletionDiagnostics> ChatCompletions => chats;
    public IReadOnlyList<ToolCallDiagnostics> ToolCalls => tools;
    public int TotalInputMessages => input.Count;
    public int TotalOutputMessages => output.Messages.Count;
    public IReadOnlyList<ChatMessage> InputMessages => input;
    public AgentResponse? OutputResponse => output;
    public bool Succeeded => true;
    public string? ErrorMessage => null;
    public DateTimeOffset StartedAt => new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public DateTimeOffset CompletedAt => StartedAt.AddMilliseconds(150);
    public string? ExecutionMode => "demo";
}
