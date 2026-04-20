// =============================================================================
// Early Completion After Tool Calls Example
// =============================================================================
// Demonstrates CheckCompletionAfterToolCalls — the opt-in feature that checks
// IsComplete after tool calls within an iteration, avoiding wasted ChatCompletion
// API calls when a tool already satisfied the completion condition.
//
// This example runs the SAME scenario three ways:
//   1. Default (None)           — IsComplete checked only between iterations
//   2. AfterToolRounds          — IsComplete checked after each round's batch
//   3. AfterEachToolCall        — IsComplete checked after each individual tool
//
// Each run prints the number of ChatCompletion calls made and the termination
// reason, showing the cost savings from early exit.
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

Console.WriteLine("=== Early Completion After Tool Calls Example ===");
Console.WriteLine();
Console.WriteLine("Scenario: An agent reads a manifest, then writes a brief.");
Console.WriteLine("IsComplete checks: workspace.FileExists(\"brief.md\")");
Console.WriteLine("The brief is written by the WriteBrief tool in the FIRST round.");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Run the scenario three ways to show the difference
// ---------------------------------------------------------------------------

await RunScenario(
    "1. Default (None)",
    ToolCompletionCheckMode.None);

Console.WriteLine(new string('-', 60));

await RunScenario(
    "2. AfterToolRounds",
    ToolCompletionCheckMode.AfterToolRounds);

Console.WriteLine(new string('-', 60));

await RunScenario(
    "3. AfterEachToolCall (3 tools requested, completion after 2nd)",
    ToolCompletionCheckMode.AfterEachToolCall);

Console.WriteLine();
Console.WriteLine("=== Summary ===");
Console.WriteLine();
Console.WriteLine("  Default (None):      2 CC calls — the 2nd is wasted (full conversation replay)");
Console.WriteLine("  AfterToolRounds:     1 CC call  — exits after tool batch, saves the wasted call");
Console.WriteLine("  AfterEachToolCall:   1 CC call  — exits mid-batch, also skips the 3rd tool");
Console.WriteLine();

return 0;

// =============================================================================
// Scenario runner
// =============================================================================

static async Task RunScenario(string label, ToolCompletionCheckMode mode)
{
    Console.WriteLine($"  [{label}]");

    var workspace = new InMemoryWorkspace();
    var mock = new BriefWriterMockChatClient(mode);

    var config = new ConfigurationBuilder().Build();
    var sp = new Syringe()
        .UsingReflection()
        .UsingAgentFramework(af => af
            .Configure(opts => opts.ChatClientFactory = _ => mock)
            .UsingDiagnostics())
        .BuildServiceProvider(config);

    var loop = sp.GetRequiredService<IIterativeAgentLoop>();

    var readManifest = AIFunctionFactory.Create(
        () =>
        {
            Console.WriteLine("    [tool] ReadManifest executed");
            return "manifest contents: { pages: [\"page1\", \"page2\"] }";
        },
        new AIFunctionFactoryOptions { Name = "ReadManifest" });

    var writeBrief = AIFunctionFactory.Create(
        () =>
        {
            Console.WriteLine("    [tool] WriteBrief executed → writes brief.md");
            workspace.TryWriteFile("brief.md", "# Research Brief\n\nAnalysis of pages 1 and 2.");
            return "brief written to brief.md";
        },
        new AIFunctionFactoryOptions { Name = "WriteBrief" });

    var summarize = AIFunctionFactory.Create(
        () =>
        {
            Console.WriteLine("    [tool] Summarize executed (unnecessary extra work)");
            return "summary complete";
        },
        new AIFunctionFactoryOptions { Name = "Summarize" });

    var options = new IterativeLoopOptions
    {
        LoopName = $"early-completion-{mode}",
        Instructions = "You are a research agent. Read the manifest, then write a brief.",
        PromptFactory = _ => "Read the manifest and write a research brief to brief.md.",
        Tools = [readManifest, writeBrief, summarize],
        MaxIterations = 5,
        ToolResultMode = ToolResultMode.MultiRound,
        MaxToolRoundsPerIteration = 5,
        IsComplete = ctx => ctx.Workspace.FileExists("brief.md"),
        CheckCompletionAfterToolCalls = mode,
    };

    var result = await loop.RunAsync(
        options,
        new IterativeContext { Workspace = workspace },
        CancellationToken.None);

    var diag = result.Diagnostics!;
    var ccCount = diag.ChatCompletions.Count;
    var toolCount = diag.ToolCalls.Count;
    var totalInputTokens = diag.ChatCompletions.Sum(c => c.Tokens.InputTokens);

    Console.WriteLine($"    CC calls:       {ccCount}");
    Console.WriteLine($"    Tools executed:  {toolCount}");
    Console.WriteLine($"    Input tokens:    {totalInputTokens}");
    Console.WriteLine($"    Termination:     {result.Termination}");
    Console.WriteLine($"    Succeeded:       {result.Succeeded}");
    Console.WriteLine();
}

// =============================================================================
// Mock chat client
// =============================================================================
// Simulates an LLM that:
//   CC[0] → requests ReadManifest + WriteBrief (+ Summarize in AfterEachToolCall mode)
//   CC[1] → returns text "Done." (the wasted call in default mode)

internal sealed class BriefWriterMockChatClient : IChatClient
{
    private readonly ToolCompletionCheckMode _mode;
    private int _callCount;

    public BriefWriterMockChatClient(ToolCompletionCheckMode mode) => _mode = mode;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var n = Interlocked.Increment(ref _callCount);
        var msgList = messages.ToList();

        Console.WriteLine($"    [CC {n - 1}] ChatCompletion called ({msgList.Count} messages)");

        if (n == 1)
        {
            var toolCalls = new List<AIContent>
            {
                new FunctionCallContent("call-read", "ReadManifest"),
                new FunctionCallContent("call-write", "WriteBrief"),
            };

            // In AfterEachToolCall mode, add a third tool to show it gets skipped
            if (_mode == ToolCompletionCheckMode.AfterEachToolCall)
            {
                toolCalls.Add(new FunctionCallContent("call-summarize", "Summarize"));
            }

            var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, toolCalls)])
            {
                ModelId = "mock-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 500,
                    OutputTokenCount = 30,
                    TotalTokenCount = 530,
                },
            };
            return Task.FromResult(response);
        }

        // Subsequent calls: return text (this is the "wasted" call in default mode)
        var textResponse = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Done.")])
        {
            ModelId = "mock-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 26000,
                OutputTokenCount = 2,
                TotalTokenCount = 26002,
            },
        };
        return Task.FromResult(textResponse);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
