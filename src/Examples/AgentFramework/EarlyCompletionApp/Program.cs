// =============================================================================
// Early Completion After Tool Calls Example
// =============================================================================
// Demonstrates CheckCompletionAfterToolCalls — the opt-in feature that checks
// IsComplete after tool calls within an iteration, avoiding wasted ChatCompletion
// API calls when a tool already satisfied the completion condition.
//
// This example runs the SAME scenario twice with a real Copilot LLM:
//   1. Default (None)       — IsComplete checked only between iterations
//   2. AfterToolRounds      — IsComplete checked after each round's batch
//
// The agent is given a simple task: read a "manifest" from the workspace, then
// write a brief summary to "brief.md". Once brief.md exists, IsComplete fires.
// In default mode, the loop makes an extra ChatCompletion call after the write
// (wasted tokens). In AfterToolRounds mode, it exits immediately.
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login` first)
//   - No API keys needed — auth flows through your GitHub OAuth token
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Copilot;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var copilotSection = configuration.GetSection("Copilot");
var copilotOptions = new CopilotChatClientOptions
{
    DefaultModel = copilotSection["Model"] ?? "gpt-4.1",
};
IChatClient chatClient = new CopilotChatClient(copilotOptions);

Console.WriteLine("=== Early Completion After Tool Calls Example ===");
Console.WriteLine();
Console.WriteLine($"  LLM:     Copilot ({copilotOptions.DefaultModel})");
Console.WriteLine($"  Task:    Read manifest → write brief.md");
Console.WriteLine($"  Check:   IsComplete = workspace.FileExists(\"brief.md\")");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Tools: ReadManifest and WriteBrief operate on the workspace
// ---------------------------------------------------------------------------

static AIFunction CreateReadManifest(IWorkspace workspace)
{
    return AIFunctionFactory.Create(
        () =>
        {
            Console.WriteLine("    [tool] ReadManifest → reading from workspace");
            return workspace.TryReadFile("manifest.json").Value.Content;
        },
        new AIFunctionFactoryOptions
        {
            Name = "ReadManifest",
            Description = "Read the project manifest from the workspace.",
        });
}

static AIFunction CreateWriteBrief(IWorkspace workspace)
{
    return AIFunctionFactory.Create(
        (string content) =>
        {
            Console.WriteLine("    [tool] WriteBrief → writing brief.md to workspace");
            workspace.TryWriteFile("brief.md", content);
            return "brief.md written successfully";
        },
        new AIFunctionFactoryOptions
        {
            Name = "WriteBrief",
            Description = "Write the research brief content to brief.md in the workspace. " +
                "The content parameter should contain the full brief text.",
        });
}

// ---------------------------------------------------------------------------
// Run 1: Default mode (None) — extra CC call happens
// ---------------------------------------------------------------------------

Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
Console.WriteLine("║  Run 1: Default (None) — no early completion check   ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
Console.WriteLine();

var result1 = await RunScenario(chatClient, ToolCompletionCheckMode.None);

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
Console.WriteLine("║  Run 2: AfterToolRounds — early exit after tool call ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
Console.WriteLine();

var result2 = await RunScenario(chatClient, ToolCompletionCheckMode.AfterToolRounds);

// ---------------------------------------------------------------------------
// Comparison
// ---------------------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== Comparison ===");
Console.WriteLine();

var diag1 = result1.Diagnostics!;
var diag2 = result2.Diagnostics!;

var cc1 = diag1.ChatCompletions.Count;
var cc2 = diag2.ChatCompletions.Count;
var tokens1 = diag1.AggregateTokenUsage.InputTokens;
var tokens2 = diag2.AggregateTokenUsage.InputTokens;

Console.WriteLine($"  {"Mode",-25} {"CC Calls",10} {"Input Tokens",15} {"Termination",-30}");
Console.WriteLine($"  {new string('-', 25)} {new string('-', 10)} {new string('-', 15)} {new string('-', 30)}");
Console.WriteLine($"  {"Default (None)",-25} {cc1,10} {tokens1,15:N0} {result1.Termination,-30}");
Console.WriteLine($"  {"AfterToolRounds",-25} {cc2,10} {tokens2,15:N0} {result2.Termination,-30}");
Console.WriteLine();

if (tokens1 > tokens2)
{
    var saved = tokens1 - tokens2;
    var pct = (double)saved / tokens1 * 100;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✓ AfterToolRounds saved {saved:N0} input tokens ({pct:F0}% reduction)");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  ⚠ No token savings observed (the model may not have made tool calls)");
    Console.ResetColor();
}

Console.WriteLine();
return 0;

// =============================================================================
// Scenario runner
// =============================================================================

static async Task<IterativeLoopResult> RunScenario(
    IChatClient chatClient,
    ToolCompletionCheckMode mode)
{
    var workspace = new InMemoryWorkspace();

    // Seed the workspace with a manifest for the agent to read
    workspace.TryWriteFile("manifest.json", """
        {
            "project": "EarlyCompletionDemo",
            "pages": [
                { "id": "intro", "title": "Introduction to Needlr" },
                { "id": "features", "title": "Key Features" },
                { "id": "perf", "title": "Performance Characteristics" }
            ],
            "goal": "Produce a 3-paragraph research brief summarizing these pages."
        }
        """);

    var config = new ConfigurationBuilder().Build();
    var sp = new Syringe()
        .UsingReflection()
        .UsingAgentFramework(af => af
            .Configure(opts => opts.ChatClientFactory = _ => chatClient)
            .UsingDiagnostics())
        .BuildServiceProvider(config);

    var loop = sp.GetRequiredService<IIterativeAgentLoop>();

    var tools = new AITool[]
    {
        CreateReadManifest(workspace),
        CreateWriteBrief(workspace),
    };

    var options = new IterativeLoopOptions
    {
        LoopName = $"early-completion-{mode}",
        Instructions = """
            You are a research assistant. Your task:
            1. Call ReadManifest to read the project manifest
            2. Based on the manifest, write a concise 3-paragraph research brief
            3. Call WriteBrief with your brief content

            You MUST call WriteBrief to save your output. Do not just respond with text.
            Once you have called WriteBrief, your work is done.
            """,
        PromptFactory = ctx =>
        {
            var hasBrief = ctx.Workspace.FileExists("brief.md");
            if (hasBrief)
            {
                return "The brief has been written. You are done.";
            }

            return "Read the manifest and write a research brief. " +
                "Call ReadManifest first, then call WriteBrief with your analysis.";
        },
        Tools = tools,
        MaxIterations = 5,
        ToolResultMode = ToolResultMode.MultiRound,
        MaxToolRoundsPerIteration = 5,
        IsComplete = ctx => ctx.Workspace.FileExists("brief.md"),
        CheckCompletionAfterToolCalls = mode,
        OnToolCall = (iteration, toolResult) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    [{toolResult.FunctionName}] {(toolResult.Succeeded ? "✓" : "✗")} " +
                $"({toolResult.Duration.TotalMilliseconds:F0}ms)");
            Console.ResetColor();
            return Task.CompletedTask;
        },
    };

    var result = await loop.RunAsync(
        options,
        new IterativeContext { Workspace = workspace },
        CancellationToken.None);

    var diag = result.Diagnostics!;
    Console.WriteLine();
    Console.WriteLine($"  Results:");
    Console.WriteLine($"    Termination:     {result.Termination}");
    Console.WriteLine($"    Succeeded:       {result.Succeeded}");
    Console.WriteLine($"    Iterations:      {result.Iterations.Count}");
    Console.WriteLine($"    CC calls:        {diag.ChatCompletions.Count}");
    Console.WriteLine($"    Tool calls:      {diag.ToolCalls.Count}");
    Console.WriteLine($"    Input tokens:    {diag.AggregateTokenUsage.InputTokens:N0}");
    Console.WriteLine($"    Output tokens:   {diag.AggregateTokenUsage.OutputTokens:N0}");

    foreach (var cc in diag.ChatCompletions)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    CC[{cc.Sequence}]: in={cc.Tokens.InputTokens:N0} " +
            $"out={cc.Tokens.OutputTokens:N0} dur={cc.Duration.TotalSeconds:F1}s");
        Console.ResetColor();
    }

    if (workspace.FileExists("brief.md"))
    {
        var brief = workspace.TryReadFile("brief.md").Value.Content;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Brief preview ({brief.Length} chars):");
        Console.ResetColor();
        var preview = brief.Length > 200 ? brief[..197] + "..." : brief;
        Console.WriteLine($"    {preview.Replace("\n", "\n    ")}");
    }

    return result;
}
