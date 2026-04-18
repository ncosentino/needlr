using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;

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
// The app uses a local mock judge IChatClient so no API keys or network access
// are required. Evaluator invocation is guarded — if the MEAI evaluation API
// shifts in a future package version, the deterministic print path still runs.
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

var judge = new MockJudgeChatClient();
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
WriteColored(
    ConsoleColor.DarkYellow,
    "  [SKIPPED] ToolCallAccuracyEvaluator is not shipped in Microsoft.Extensions.AI.Evaluation.Quality 9.6.0.");
WriteColored(
    ConsoleColor.DarkYellow,
    "    The trajectory emitted by IterationRecord.ToToolCallTrajectory() above is the");
WriteColored(
    ConsoleColor.DarkYellow,
    "    exact MEAI-native shape such an evaluator would consume when it becomes available.");

PrintHeader("Done");
return 0;

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
// Mock judge — returns a canned JSON response that mimics the shape
// Microsoft.Extensions.AI.Evaluation judge prompts expect. Real evaluators
// may parse this strictly; the guarded TryRunEvaluatorAsync keeps the example
// always-runnable if the schema shifts.
// =============================================================================

internal sealed class MockJudgeChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        const string cannedJson = """
            {
              "relevance": {
                "rating": 4,
                "rationale": "The assistant answered the question directly and concisely."
              },
              "toolCallAccuracy": {
                "rating": 5,
                "rationale": "Tool calls align with the user's request."
              }
            }
            """;

        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant, cannedJson),
        ])
        {
            ModelId = "mock-judge",
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
