// ============================================================================
// Copilot Comparison Example
//
// Demonstrates the same research query executed two ways:
//   1. Needlr Copilot — CopilotChatClient + CopilotWebSearchFunction
//      (direct HTTP, structured WebSearchResult with citations)
//   2. GitHub Copilot SDK — CopilotClient session with built-in web_search
//      (full agent loop via CLI process)
//
// Both use the same Copilot subscription and hit the same backend.
// The difference is what your APPLICATION CODE can observe.
//
// Requirements:
//   - GitHub Copilot CLI authenticated (run `gh auth login`)
//   - No API keys needed
// ============================================================================

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.Copilot;

var query = "What is the latest LTS version of Node.js?";
Console.WriteLine($"Query: \"{query}\"");
Console.WriteLine(new string('═', 70));

// ════════════════════════════════════════════════════════════════════════
// APPROACH 1: Needlr Copilot
//
// - CopilotChatClient: lightweight IChatClient, direct HTTP to Copilot API
// - CopilotWebSearchFunction: calls the MCP web_search tool directly
// - You get back a WebSearchResult with structured Citations and SearchQueries
// - Your code can programmatically inspect source URLs
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  APPROACH 1: Needlr Copilot (direct HTTP, structured results)   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var tools = CopilotToolSet.Create(opts => opts.EnableWebSearch = true);
var webSearch = tools.First(t => t.Name == "web_search");

var funcArgs = new AIFunctionArguments(
    new Dictionary<string, object?> { ["query"] = query });

var needlrResult = await webSearch.InvokeAsync(funcArgs);

if (needlrResult is WebSearchResult searchResult)
{
    Console.WriteLine($"  Runtime type: WebSearchResult ✅");
    Console.WriteLine($"  Text length:  {searchResult.Text.Length} chars");
    Console.WriteLine($"  Citations:    {searchResult.Citations.Count}");
    Console.WriteLine($"  Bing queries: {searchResult.SearchQueries.Count}");
    Console.WriteLine();

    if (searchResult.Citations.Count > 0)
    {
        Console.WriteLine("  Source URLs (from structured Citations):");
        foreach (var citation in searchResult.Citations)
        {
            Console.WriteLine($"    [{citation.Title}]");
            Console.WriteLine($"    {citation.Url}");
            Console.WriteLine($"    (chars {citation.StartIndex}-{citation.EndIndex})");
            Console.WriteLine();
        }
    }
    else
    {
        Console.WriteLine("  ⚠ No citations — LLM answered from training data, not web search.");
        Console.WriteLine("  The text may still be accurate, but URLs are not verifiable.");
    }

    Console.WriteLine("  Answer (first 200 chars):");
    Console.WriteLine($"  {searchResult.Text[..Math.Min(200, searchResult.Text.Length)]}...");
}
else
{
    Console.WriteLine($"  Result type: {needlrResult?.GetType().Name ?? "null"}");
    Console.WriteLine($"  Value: {needlrResult}");
}

// ════════════════════════════════════════════════════════════════════════
// APPROACH 2: GitHub Copilot SDK
//
// - CopilotClient spawns the Copilot CLI as a child process (JSON-RPC)
// - The agent decides which tools to call (web_search, web_fetch, etc.)
// - You get back the agent's final text response
// - The agent SAW the citations internally, but your code only gets text
// - The agent can chain tools (search → fetch → synthesize)
//
// NOTE: The SDK is in public preview and the API surface is evolving.
// The code below shows the documented pattern. If your installed SDK
// version differs, consult https://github.com/github/copilot-sdk/tree/main/dotnet
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  APPROACH 2: GitHub Copilot SDK (full agent loop via CLI)        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

try
{
    await using var copilotClient = new GitHub.Copilot.SDK.CopilotClient();
    await using var session = await copilotClient.CreateSessionAsync(
        new GitHub.Copilot.SDK.SessionConfig
        {
            Model = "claude-sonnet-4",
            OnPermissionRequest = GitHub.Copilot.SDK.PermissionHandler.ApproveAll,
        });

    var toolLog = new List<string>();

    // Log full tool call diagnostics — arguments on start, results on complete
    using var _ = session.On(e =>
    {
        if (e is GitHub.Copilot.SDK.ToolExecutionStartEvent start)
        {
            var argsJson = start.Data.Arguments is { } args
                ? System.Text.Json.JsonSerializer.Serialize(args,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                : "(none)";
            var entry = $"  🔧 TOOL START: {start.Data.ToolName}\n" +
                        $"     Arguments: {argsJson}";
            Console.WriteLine(entry);
            toolLog.Add(entry);
        }
        else if (e is GitHub.Copilot.SDK.ToolExecutionCompleteEvent complete)
        {
            var resultText = "(none)";
            try
            {
                if (complete.Data.Result is { } result)
                {
                    resultText = System.Text.Json.JsonSerializer.Serialize(result,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    if (resultText.Length > 500)
                        resultText = resultText[..500] + "... (truncated)";
                }
            }
            catch
            {
                resultText = complete.Data.Result?.ToString() ?? "(none)";
            }

            var errorText = complete.Data.Error is { } err
                ? System.Text.Json.JsonSerializer.Serialize(err)
                : null;

            var entry = $"  ✅ TOOL DONE:  {complete.Data.ToolCallId}\n" +
                        $"     Success: {complete.Data.Success}" +
                        (errorText != null ? $"\n     Error: {errorText}" : "") +
                        $"\n     Result: {resultText}";
            Console.WriteLine(entry);
            toolLog.Add(entry);
        }
    });

    Console.WriteLine("  Sending query to SDK agent...");
    Console.WriteLine();

    var reply = await session.SendAndWaitAsync(
        new GitHub.Copilot.SDK.MessageOptions
        {
            Prompt = $"Search the web and answer: {query}. " +
                     "Include source URLs in your response.",
        });

    var sdkText = reply?.Data?.Content ?? "(no response)";
    Console.WriteLine();
    Console.WriteLine($"  ─── SDK RESPONSE ───");
    Console.WriteLine($"  Length: {sdkText.Length} chars");
    Console.WriteLine($"  Tool calls observed: {toolLog.Count / 2}");
    Console.WriteLine();
    Console.WriteLine($"  Full answer:");
    Console.WriteLine($"  {sdkText}");
}
catch (Exception ex)
{
    Console.WriteLine($"  SDK error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("  (The SDK requires the Copilot CLI and the API surface may differ");
    Console.WriteLine("   between preview versions. See the SDK README for your version.)");
}

// ════════════════════════════════════════════════════════════════════════
// COMPARISON SUMMARY
// ════════════════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine(new string('═', 70));
Console.WriteLine("COMPARISON SUMMARY");
Console.WriteLine(new string('═', 70));
Console.WriteLine();
Console.WriteLine("Needlr Copilot:");
Console.WriteLine("  ✅ Structured WebSearchResult with Citations, URLs, offsets");
Console.WriteLine("  ✅ Lightweight — no CLI process, pure HTTP");
Console.WriteLine("  ✅ CopilotRateLimitException for provider fallback");
Console.WriteLine("  ❌ Only web_search — no file editing, code search, web_fetch");
Console.WriteLine("  ❌ LLM decides whether to actually search (no guarantee)");
Console.WriteLine();
Console.WriteLine("GitHub Copilot SDK:");
Console.WriteLine("  ✅ Full agent loop — web_search, web_fetch, file ops, bash, etc.");
Console.WriteLine("  ✅ Agent can chain tools (search → fetch page → synthesize)");
Console.WriteLine("  ✅ Officially supported, MAF integration, session persistence");
Console.WriteLine("  ❌ No structured citation access from your code");
Console.WriteLine("  ❌ ~100MB CLI binary bundled in NuGet package");
Console.WriteLine("  ❌ Same LLM-mediated search (no guaranteed web grounding)");
