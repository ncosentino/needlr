// =============================================================================
// Iterative Loop Diagnostics Example
// =============================================================================
// Demonstrates that IIterativeAgentLoop + UsingDiagnostics() produces exactly
// ONE ChatCompletionDiagnostics entry per LLM call — no duplication.
//
// This is a living canary for the ChatCompletion duplication bug (alpha-42).
// If this example prints 2× entries, the bug has regressed.
//
// Uses a mock chat client — no LLM credentials required.
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

Console.WriteLine("=== Iterative Loop Diagnostics Example ===");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 1. Build service provider with UsingDiagnostics() + mock chat client
// ---------------------------------------------------------------------------

Console.WriteLine("[SETUP] Wiring UsingAgentFramework + UsingDiagnostics with mock chat client");

var config = new ConfigurationBuilder().Build();
var mockChat = new ToolCallingMockChatClient();

var sp = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .Configure(opts => opts.ChatClientFactory = _ => mockChat)
        .UsingDiagnostics())
    .BuildServiceProvider(config);

var loop = sp.GetRequiredService<IIterativeAgentLoop>();

Console.WriteLine("[SETUP] IIterativeAgentLoop resolved from DI");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 2. Run the iterative loop (2 actual LLM calls: tool call + final text)
// ---------------------------------------------------------------------------

Console.WriteLine("[RUN] Running iterative loop with 1 tool call...");

var searchTool = AIFunctionFactory.Create(
    () => "Found 3 articles about diagnostics deduplication.",
    new AIFunctionFactoryOptions { Name = "SearchArticles" });

var options = new IterativeLoopOptions
{
    Instructions = "You are a research assistant. Search for articles when asked.",
    PromptFactory = _ => "Search for articles about diagnostics deduplication in agent frameworks.",
    Tools = [searchTool],
    MaxIterations = 3,
    IsComplete = _ => true,
    LoopName = "dedup-canary",
};

var result = await loop.RunAsync(
    options,
    new IterativeContext { Workspace = new InMemoryWorkspace() },
    CancellationToken.None);

Console.WriteLine($"[RUN] Loop completed: {result.Iterations.Count} iteration(s)");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 3. Inspect diagnostics — this is the critical check
// ---------------------------------------------------------------------------

var diag = result.Diagnostics;

Console.WriteLine("=== DIAGNOSTICS INSPECTION ===");
Console.WriteLine();
Console.WriteLine($"  ChatCompletions.Count:  {diag!.ChatCompletions.Count}");
Console.WriteLine($"  ToolCalls.Count:        {diag!.ToolCalls.Count}");
Console.WriteLine($"  Aggregate TotalTokens:  {diag!.AggregateTokenUsage.TotalTokens}");
Console.WriteLine();

Console.WriteLine("  --- Per-completion breakdown ---");
foreach (var cc in diag!.ChatCompletions)
{
    Console.WriteLine($"    Seq {cc.Sequence}: in={cc.Tokens.InputTokens}, " +
        $"out={cc.Tokens.OutputTokens}, total={cc.Tokens.TotalTokens}, " +
        $"dur={cc.Duration.TotalMilliseconds:F0}ms, model={cc.Model}");
}

Console.WriteLine();

var uniqueSum = diag!.ChatCompletions.Sum(c => c.Tokens.TotalTokens);
Console.WriteLine($"  Sum of all entries:     {uniqueSum}");
Console.WriteLine($"  AggregateTokenUsage:    {diag!.AggregateTokenUsage.TotalTokens}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 4. Verify: no duplication
// ---------------------------------------------------------------------------

Console.WriteLine("=== VERIFICATION ===");
Console.WriteLine();

// The mock produces 2 LLM calls (1 tool-call response + 1 text response)
var expectedLlmCalls = 2;
var allSequencesUnique = diag!.ChatCompletions
    .Select(c => c.Sequence)
    .Distinct()
    .Count() == diag!.ChatCompletions.Count;
var tokensMatch = uniqueSum == diag!.AggregateTokenUsage.TotalTokens;

var passed = true;

if (diag!.ChatCompletions.Count != expectedLlmCalls)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ✗ FAILED — Expected {expectedLlmCalls} ChatCompletions, got {diag!.ChatCompletions.Count}");
    Console.WriteLine($"             If this is 2× expected, the duplication bug has regressed.");
    Console.ResetColor();
    passed = false;
}
else
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✓ ChatCompletions.Count == {expectedLlmCalls} (correct, no duplication)");
    Console.ResetColor();
}

if (!allSequencesUnique)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ✗ FAILED — Duplicate sequence numbers detected");
    Console.ResetColor();
    passed = false;
}
else
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✓ All sequence numbers unique");
    Console.ResetColor();
}

if (!tokensMatch)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ✗ FAILED — AggregateTokenUsage ({diag!.AggregateTokenUsage.TotalTokens}) " +
        $"!= sum of entries ({uniqueSum})");
    Console.ResetColor();
    passed = false;
}
else
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✓ AggregateTokenUsage matches sum of unique entries");
    Console.ResetColor();
}

if (diag!.ToolCalls.Count != 1)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ✗ FAILED — Expected 1 ToolCall, got {diag!.ToolCalls.Count}");
    Console.ResetColor();
    passed = false;
}
else
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✓ ToolCalls.Count == 1 (correct)");
    Console.ResetColor();
}

Console.WriteLine();
if (passed)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("All checks passed — no duplication detected.");
    Console.ResetColor();
    return 0;
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("CHECKS FAILED — duplication bug may have regressed.");
    Console.ResetColor();
    return 1;
}

// =============================================================================
// Mock chat client — returns a tool call on first request, text on second
// =============================================================================

internal sealed class ToolCallingMockChatClient : IChatClient
{
    private int _callCount;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var n = Interlocked.Increment(ref _callCount);

        if (n == 1)
        {
            // First call: return a tool call
            var response = new ChatResponse(
            [
                new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent("call-1", "SearchArticles",
                        new Dictionary<string, object?> { ["query"] = "diagnostics" })
                ])
            ])
            {
                ModelId = "mock-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 150,
                    OutputTokenCount = 30,
                    TotalTokenCount = 180,
                },
            };
            return Task.FromResult(response);
        }

        // Second+ call: return text (iteration complete)
        var textResponse = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Here are the results from the search.")])
        {
            ModelId = "mock-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 200,
                OutputTokenCount = 80,
                TotalTokenCount = 280,
            },
        };
        return Task.FromResult(textResponse);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming not used by IterativeAgentLoop");

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
