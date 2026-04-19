// ============================================================================
// Copilot Comparison Example
//
// Demonstrates the SAME research query executed through TWO approaches, both
// using Needlr's Syringe DI container and agent framework:
//
//   Approach 1: Needlr Copilot — CopilotChatClient as IChatClient, web_search
//               exposed as an AIFunction tool, iterative agent loop runs the
//               query. Your code gets structured WebSearchResult with citations.
//
//   Approach 2: GitHub Copilot SDK — CopilotClient session with the CLI's
//               built-in tools. The agent decides which tools to use. Your code
//               gets the final text response and tool execution events.
//
// Both approaches use the same Copilot subscription and hit the same backend.
// The difference is what your APPLICATION CODE can observe.
//
// Requirements:
//   - GitHub Copilot CLI authenticated (run `gh auth login`)
//   - No API keys needed
// ============================================================================

using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Copilot;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

var query = "What is the latest LTS version of Node.js and where can I download it?";

Console.WriteLine($"Query: \"{query}\"");
Console.WriteLine(new string('═', 70));

// ════════════════════════════════════════════════════════════════════════
// APPROACH 1: Needlr Copilot + Syringe + Iterative Agent Loop
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  APPROACH 1: Needlr Syringe + CopilotChatClient + web_search   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var configuration = new ConfigurationBuilder().Build();
var copilotOptions = new CopilotChatClientOptions { DefaultModel = "claude-sonnet-4" };
IChatClient chatClient = new CopilotChatClient(copilotOptions);
var copilotTools = CopilotToolSet.Create(t => t.EnableWebSearch = true);

// Wire everything through Syringe — the same DI setup a real app would use
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient))
    .BuildServiceProvider(configuration);

var iterativeLoop = serviceProvider.GetRequiredService<IIterativeAgentLoop>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

var loopOptions = new IterativeLoopOptions
{
    Instructions = "You are a research assistant. Use the web_search tool to find current information. " +
                   "Always include source URLs from your search results in your answer.",
    PromptFactory = _ => query,
    Tools = copilotTools,
    MaxIterations = 1,
    ToolResultMode = ToolResultMode.OneRoundTrip,
    LoopName = "needlr-copilot-research",
    OnToolCall = (_, toolCall) =>
    {
        Console.WriteLine($"  🔧 Tool: {toolCall.FunctionName}");
        var resultStr = ToolResultSerializer.Serialize(toolCall.Result);
        Console.WriteLine($"     Result: {resultStr.Length} chars");

        // Demonstrate structured access — this is what the SDK CAN'T do
        if (toolCall.Result is WebSearchResult wsr)
        {
            Console.WriteLine($"     Runtime type: WebSearchResult ✅");
            Console.WriteLine($"     Citations: {wsr.Citations.Count}");
            foreach (var c in wsr.Citations)
                Console.WriteLine($"       [{c.Title}] → {c.Url}");
            Console.WriteLine($"     Bing queries: {wsr.SearchQueries.Count}");
        }

        return Task.CompletedTask;
    },
};

using var diagnosticsScope = diagnosticsAccessor.BeginCapture();

var context = new IterativeContext { Workspace = new InMemoryWorkspace() };
var result = await iterativeLoop.RunAsync(loopOptions, context, CancellationToken.None);

var finalText = result.FinalResponse?.Text ?? "(no response)";
Console.WriteLine();
Console.WriteLine($"  ─── Agent Response ───");
Console.WriteLine($"  {finalText[..Math.Min(300, finalText.Length)]}...");

// Show diagnostics — prove the LLM saw the tool result
var diagnostics = diagnosticsAccessor.LastRunDiagnostics;
if (diagnostics is not null)
{
    Console.WriteLine();
    Console.WriteLine($"  ─── Diagnostics ───");
    Console.WriteLine($"  Chat completions: {diagnostics.ChatCompletions.Count}");
    Console.WriteLine($"  Tool calls: {diagnostics.ToolCalls.Count}");
    foreach (var tc in diagnostics.ToolCalls)
    {
        Console.WriteLine($"    {tc.ToolName}: {(tc.Succeeded ? "✅" : "❌")} " +
                          $"({tc.ResultCharCount} chars result)");
    }
}

// ════════════════════════════════════════════════════════════════════════
// APPROACH 2: GitHub Copilot SDK — Same Query via CLI Agent Loop
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  APPROACH 2: GitHub Copilot SDK (full CLI agent loop)            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

try
{
    await using var sdkClient = new GitHub.Copilot.SDK.CopilotClient();
    await using var session = await sdkClient.CreateSessionAsync(
        new GitHub.Copilot.SDK.SessionConfig
        {
            Model = "claude-sonnet-4",
            OnPermissionRequest = GitHub.Copilot.SDK.PermissionHandler.ApproveAll,
        });

    using var _ = session.On(e =>
    {
        if (e is GitHub.Copilot.SDK.ToolExecutionStartEvent start)
        {
            var argsJson = start.Data.Arguments is { } args
                ? JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false })
                : "(none)";
            Console.WriteLine($"  🔧 Tool: {start.Data.ToolName}");
            Console.WriteLine($"     Args: {argsJson}");
        }
        else if (e is GitHub.Copilot.SDK.ToolExecutionCompleteEvent complete)
        {
            var resultJson = "(none)";
            try
            {
                if (complete.Data.Result is { } r)
                {
                    resultJson = JsonSerializer.Serialize(r,
                        new JsonSerializerOptions { WriteIndented = false });
                    if (resultJson.Length > 200)
                        resultJson = resultJson[..200] + "... (truncated)";
                }
            }
            catch
            {
                resultJson = complete.Data.Result?.ToString() ?? "(none)";
            }

            Console.WriteLine($"  ✅ Done:  {complete.Data.ToolCallId}");
            Console.WriteLine($"     Success: {complete.Data.Success}");
            Console.WriteLine($"     Result: {resultJson}");
        }
    });

    var reply = await session.SendAndWaitAsync(
        new GitHub.Copilot.SDK.MessageOptions
        {
            Prompt = query + " Use the web to find current information. Include source URLs.",
        });

    var sdkText = reply?.Data?.Content ?? "(no response)";
    Console.WriteLine();
    Console.WriteLine($"  ─── Agent Response ───");
    Console.WriteLine($"  {sdkText[..Math.Min(300, sdkText.Length)]}...");
}
catch (Exception ex)
{
    Console.WriteLine($"  SDK error: {ex.GetType().Name}: {ex.Message}");
}

// ════════════════════════════════════════════════════════════════════════
// COMPARISON SUMMARY
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine(new string('═', 70));
Console.WriteLine("COMPARISON SUMMARY");
Console.WriteLine(new string('═', 70));
Console.WriteLine();
Console.WriteLine("Both approaches used the same Copilot subscription, same query,");
Console.WriteLine("same model. The key differences:");
Console.WriteLine();
Console.WriteLine("Needlr Copilot (Approach 1):");
Console.WriteLine("  ✅ Syringe DI → IIterativeAgentLoop → CopilotChatClient");
Console.WriteLine("  ✅ web_search exposed as AIFunction tool");
Console.WriteLine("  ✅ Structured WebSearchResult with Citations, URLs, offsets");
Console.WriteLine("  ✅ Diagnostics middleware captures tool call details");
Console.WriteLine("  ✅ Lightweight — no CLI process, pure HTTP");
Console.WriteLine("  ❌ Only the tools you explicitly provide");
Console.WriteLine();
Console.WriteLine("GitHub Copilot SDK (Approach 2):");
Console.WriteLine("  ✅ Full CLI agent loop with ALL built-in tools");
Console.WriteLine("  ✅ Agent can chain tools (web_search → web_fetch → synthesize)");
Console.WriteLine("  ✅ Tool events visible via session.On()");
Console.WriteLine("  ❌ No structured citation access from your code");
Console.WriteLine("  ❌ Not wired through Syringe — standalone SDK client");
Console.WriteLine("  ❌ ~100MB CLI binary bundled in NuGet package");
