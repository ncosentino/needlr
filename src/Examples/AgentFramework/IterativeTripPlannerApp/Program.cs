using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

using IterativeTripPlannerApp.Core;

using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.Copilot;

// ============================================================================
// Iterative Trip Planner — Console Entry Point
//
// Thin entry point that demonstrates the IterativeTripPlannerApp.Core library.
// All trip planning logic lives in Core; this file handles configuration,
// Copilot wiring, console output hooks, and result rendering.
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login` first)
//   - No API keys needed — auth flows through your GitHub OAuth token
// ============================================================================

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var tripSection = configuration.GetSection("TripPlanner");
var config = new TripPlannerConfig(
    Origin: tripSection["Origin"] ?? "New York",
    Destination: tripSection["Destination"] ?? "Tokyo",
    MaxStops: int.Parse(tripSection["MaxStops"] ?? "5"),
    MinStops: int.Parse(tripSection["MinStops"] ?? "3"),
    Budget: tripSection["Budget"] ?? "3000");

// ── Copilot as the LLM provider ────────────────────────────────────────
var copilotSection = configuration.GetSection("Copilot");
var copilotOptions = new CopilotChatClientOptions
{
    DefaultModel = copilotSection["Model"] ?? "claude-sonnet-4",
};
IChatClient chatClient = new CopilotChatClient(copilotOptions);
Console.WriteLine($"Using Copilot chat client (model: {copilotOptions.DefaultModel})");

var copilotTools = CopilotToolSet.Create(t => t.EnableWebSearch = true);
Console.WriteLine($"Copilot tools enabled: {string.Join(", ", copilotTools.Select(t => t.Name))}");
Console.WriteLine();

// ── Console output hooks ────────────────────────────────────────────────
var iterationStopwatch = System.Diagnostics.Stopwatch.StartNew();

var hooks = new TripPlannerHooks
{
    OnIterationStart = (iteration, ctx) =>
    {
        if (iteration > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  └─ iteration {iteration - 1} complete ({iterationStopwatch.ElapsedMilliseconds}ms)");
            Console.ResetColor();
            Console.WriteLine();
        }
        iterationStopwatch.Restart();

        var statusJson = ctx.Workspace.TryReadFile("status.json").Value.Content;
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;
        var phase = status.GetValueOrDefault("phase", "research").ToString()!.ToUpperInvariant();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"▶ Iteration {iteration}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [{phase}]");
        Console.ResetColor();

        var workspaceSize = ctx.Workspace.GetFilePaths().Sum(p => ctx.Workspace.TryReadFile(p).Value.Content.Length);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  (workspace: {workspaceSize:N0} chars across {ctx.Workspace.GetFilePaths().Count()} files)");
        Console.ResetColor();

        return Task.CompletedTask;
    },

    OnToolCall = (iteration, toolCallResult) =>
    {
        var name = toolCallResult.FunctionName;
        var resultStr = toolCallResult.Result?.ToString() ?? "(null)";

        var summary = name switch
        {
            "web_search" => resultStr.Length > 100 ? resultStr[..97] + "..." : resultStr,
            "AddLeg" => resultStr,
            "RemoveLeg" => resultStr,
            "BookHotel" => resultStr,
            "RemoveHotel" => resultStr,
            "ValidateTrip" => resultStr.Contains("\"VALID\"") ? "✓ VALID" : $"✗ issues found",
            "FinalizeTrip" => "Trip finalized and summary saved.",
            _ => resultStr.Length > 80 ? resultStr[..77] + "..." : resultStr,
        };

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"  ├─ {name}");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" → {summary}");
        Console.ResetColor();

        return Task.CompletedTask;
    },

    OnIterationEnd = (_) => Task.CompletedTask,
};

// ── Print banner ────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   ITERATIVE TRIP PLANNER — Copilot + Web Search             ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Origin:       {config.Origin,-45}║");
Console.WriteLine($"║  Destination:  {config.Destination,-45}║");
Console.WriteLine($"║  Budget:       ${config.Budget,-44}║");
Console.WriteLine($"║  Min stops:    {config.MinStops} intermediate cities ({config.MinStops + 1}+ legs required){"",-13}║");
Console.WriteLine($"║  Max stops:    {config.MaxStops,-45}║");
Console.WriteLine($"║  Hotels:       Required in every layover city (3.5★ min)   ║");
Console.WriteLine($"║  LLM:         Copilot ({copilotOptions.DefaultModel}){"",-24}║");
Console.WriteLine($"║  Web search:   Copilot MCP (real web data)                 ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── Run ─────────────────────────────────────────────────────────────────
var runner = new TripPlannerRunner(chatClient, copilotTools);
var runResult = await runner.RunAsync(config, hooks, CancellationToken.None);

var result = runResult.LoopResult;
var accessorDiagnostics = runResult.Diagnostics;

// Close the last iteration's timing
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  └─ iteration {result.Iterations.Count - 1} complete ({iterationStopwatch.ElapsedMilliseconds}ms)");
Console.ResetColor();

// ── Diagnostics from IAgentDiagnosticsAccessor ──────────────────────────
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║             DIAGNOSTICS (from accessor)                     ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
if (accessorDiagnostics != null)
{
    Console.WriteLine($"║  Agent name:     {accessorDiagnostics.AgentName,-43}║");
    Console.WriteLine($"║  Succeeded:      {accessorDiagnostics.Succeeded,-43}║");
    Console.WriteLine($"║  Duration:       {accessorDiagnostics.TotalDuration.TotalSeconds:F1}s{"",-40}║");
    Console.WriteLine($"║  LLM calls:      {accessorDiagnostics.ChatCompletions.Count,-43}║");
    Console.WriteLine($"║  Tool calls:     {accessorDiagnostics.ToolCalls.Count,-43}║");
    Console.WriteLine($"║  Input tokens:   {accessorDiagnostics.AggregateTokenUsage.InputTokens,-43:N0}║");
    Console.WriteLine($"║  Output tokens:  {accessorDiagnostics.AggregateTokenUsage.OutputTokens,-43:N0}║");
}
else
{
    Console.WriteLine("║  (no diagnostics available)                                ║");
}
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── Per-iteration diagnostics table ─────────────────────────────────────
Console.WriteLine("╔═══════════╦═══════════════╦═══════════════╦═══════╦═══════════╦═════════════════════════════════╗");
Console.WriteLine("║ Iteration ║   Input Tok   ║  Output Tok   ║ Tools ║  Duration ║ Tool Calls                      ║");
Console.WriteLine("╠═══════════╬═══════════════╬═══════════════╬═══════╬═══════════╬═════════════════════════════════╣");
foreach (var iter in result.Iterations)
{
    var toolNames = string.Join(", ", iter.ToolCalls.Select(t => t.FunctionName));
    if (toolNames.Length > 33) toolNames = toolNames[..30] + "...";
    if (toolNames.Length == 0) toolNames = "(text response)";
    Console.WriteLine($"║ {iter.Iteration,9} ║ {iter.Tokens.InputTokens,13:N0} ║ {iter.Tokens.OutputTokens,13:N0} ║ {iter.ToolCalls.Count,5} ║ {iter.Duration.TotalMilliseconds,7:F0}ms ║ {toolNames,-31} ║");
}

Console.WriteLine("╠═══════════╬═══════════════╬═══════════════╬═══════╬═══════════╬═════════════════════════════════╣");
var totalIn = result.Iterations.Sum(i => i.Tokens.InputTokens);
var totalOut = result.Iterations.Sum(i => i.Tokens.OutputTokens);
var totalTools = result.Iterations.Sum(i => i.ToolCalls.Count);
var totalDuration = result.Iterations.Sum(i => i.Duration.TotalMilliseconds);
Console.WriteLine($"║ {"TOTAL",9} ║ {totalIn,13:N0} ║ {totalOut,13:N0} ║ {totalTools,5} ║ {totalDuration,7:F0}ms ║                                 ║");
Console.WriteLine("╚═══════════╩═══════════════╩═══════════════╩═══════╩═══════════╩═════════════════════════════════╝");
Console.WriteLine();

// ── Detailed tool call log ──────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                   DETAILED TOOL CALL LOG                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
foreach (var iter in result.Iterations)
{
    if (iter.ToolCalls.Count == 0) continue;
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  Iteration {iter.Iteration} ({iter.ToolCalls.Count} tool calls, {iter.Duration.TotalMilliseconds:F0}ms, {iter.LlmCallCount} LLM calls)");
    Console.ResetColor();
    foreach (var tc in iter.ToolCalls)
    {
        var statusIcon = tc.Succeeded ? "✓" : "✗";
        Console.ForegroundColor = tc.Succeeded ? ConsoleColor.Green : ConsoleColor.Red;
        Console.Write($"    {statusIcon} ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{tc.FunctionName}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  ({tc.Duration.TotalMilliseconds:F0}ms)");
        Console.ResetColor();

        if (tc.Arguments.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("      Args: ");
            Console.ResetColor();
            foreach (var (key, value) in tc.Arguments)
            {
                var valStr = value?.ToString() ?? "null";
                if (valStr.Length > 60) valStr = valStr[..57] + "...";
                Console.WriteLine($"        {key} = {valStr}");
            }
        }

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("      Result: ");
        Console.ResetColor();
        var resultStr = tc.Result?.ToString() ?? "(null)";
        if (resultStr.Length > 120)
        {
            Console.WriteLine(resultStr[..117] + "...");
        }
        else
        {
            Console.WriteLine(resultStr);
        }

        if (!tc.Succeeded && tc.ErrorMessage is not null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"      Error: {tc.ErrorMessage}");
            Console.ResetColor();
        }
    }
    Console.WriteLine();
}
Console.WriteLine();

// ── O(n²) vs O(n) comparison ────────────────────────────────────────────
var iterCount = result.Iterations.Count;
var avgInputPerIter = iterCount > 0 ? totalIn / iterCount : 0;

var ficBaseCost = avgInputPerIter;
var ficGrowthPerCall = avgInputPerIter / 3;
long ficTotal = 0;
long ficPeak = 0;
var totalLlmCalls = result.Iterations.Sum(i => i.LlmCallCount);
for (int k = 0; k < totalLlmCalls; k++)
{
    var callCost = ficBaseCost + (k * ficGrowthPerCall);
    ficTotal += callCost;
    ficPeak = callCost;
}

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          TOKEN COST COMPARISON: O(n) vs O(n²)              ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  LLM calls made:             {totalLlmCalls,8}                       ║");
Console.WriteLine($"║  Tool calls made:            {totalTools,8}                       ║");
Console.WriteLine($"║                                                            ║");
Console.WriteLine($"║  ITERATIVE LOOP (this run):                                ║");
Console.WriteLine($"║    Total input tokens:       {totalIn,8:N0}                       ║");
Console.WriteLine($"║    Avg per iteration:        {avgInputPerIter,8:N0}                       ║");
Console.WriteLine($"║    Peak single call:         {(result.Iterations.Count > 0 ? result.Iterations.Max(i => i.Tokens.InputTokens) : 0),8:N0}                       ║");
Console.WriteLine($"║                                                            ║");
Console.WriteLine($"║  FIC ESTIMATE (same workload, O(n²) accumulation):         ║");
Console.WriteLine($"║    Estimated total tokens:   {ficTotal,8:N0}                       ║");
Console.WriteLine($"║    Estimated peak call:      {ficPeak,8:N0}                       ║");
Console.WriteLine($"║                                                            ║");
var savings = ficTotal > 0 ? (1.0 - ((double)totalIn / ficTotal)) * 100 : 0;
Console.WriteLine($"║  SAVINGS:                    {savings,7:F1}%                       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── Final workspace state ───────────────────────────────────────────────
Console.WriteLine($"Result: {(result.Succeeded ? "SUCCESS" : $"FAILED: {result.ErrorMessage}")}");
Console.WriteLine($"Iterations: {result.Iterations.Count}");
var finalText = result.FinalResponse?.Text ?? string.Empty;
if (finalText.Length > 0)
    Console.WriteLine($"Final response: {finalText[..Math.Min(300, finalText.Length)]}");
Console.WriteLine();

Console.WriteLine("═══ Final Workspace Files ═══");
foreach (var file in runResult.Workspace.GetFilePaths().OrderBy(f => f))
{
    var content = runResult.Workspace.TryReadFile(file).Value.Content;
    var preview = content.Length > 200 ? content[..197] + "..." : content;
    Console.WriteLine($"  📄 {file} ({content.Length:N0} chars)");
    foreach (var line in preview.Split('\n').Take(6))
    {
        Console.WriteLine($"     {line.TrimEnd()}");
    }
    Console.WriteLine();
}
