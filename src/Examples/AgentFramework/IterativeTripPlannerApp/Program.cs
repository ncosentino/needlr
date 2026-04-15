using System.Text;
using System.Text.Json;

using Azure;
using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using IterativeTripPlannerApp;

// ============================================================================
// Iterative Trip Planner — IIterativeAgentLoop Example
//
// This example demonstrates the FULL integration surface of the iterative
// agent loop, mirroring real-world consumer patterns like BrandGhost:
//
//   ✓ DI-resolved tool classes with [AgentFunction] + [AgentFunctionGroup]
//   ✓ Tools access workspace via IAgentExecutionContextAccessor (not closures)
//   ✓ Tools resolved via IAgentFactory.ResolveTools() (not AIFunctionFactory)
//   ✓ Lifecycle hooks for progress reporting (not in-tool console output)
//   ✓ Diagnostics via IAgentDiagnosticsAccessor (not IterationRecord.Tokens)
//   ✓ Explicit ExecutionContext bridging on IterativeLoopOptions
//   ✓ Full Syringe DI with UsingAgentFramework() + UsingDiagnostics()
//
// The scenario: a multi-stop trip planner that researches, builds, validates,
// and finalizes an itinerary across multiple iterations with ~10+ tool calls.
// Each iteration builds a FRESH prompt from workspace state — O(n) tokens.
// ============================================================================

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var tripConfig = configuration.GetSection("TripPlanner");
var useMock = tripConfig["UseMockClient"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;

IChatClient chatClient;
if (useMock)
{
    Console.WriteLine("Using MOCK chat client (set TripPlanner:UseMockClient=false for real API)");
    Console.WriteLine("Mock simulates multi-phase trip planning with budget challenges");
    chatClient = new MockTripPlannerChatClient();
}
else
{
    var azureSection = configuration.GetSection("AzureOpenAI");
    chatClient = new AzureOpenAIClient(
            new Uri(azureSection["Endpoint"]
                ?? throw new InvalidOperationException("No AzureOpenAI:Endpoint set")),
            new AzureKeyCredential(azureSection["ApiKey"]
                ?? throw new InvalidOperationException("No AzureOpenAI:ApiKey set")))
        .GetChatClient(azureSection["DeploymentName"]
            ?? throw new InvalidOperationException("No AzureOpenAI:DeploymentName set"))
        .AsIChatClient();
}

Console.WriteLine();

// ── DI setup — mirrors real consumer patterns ───────────────────────────
// UsingAgentFramework() sets up the agent factory and iterative loop.
// AddAgentFunctionGroupsFromAssemblies() discovers [AgentFunctionGroup]
// classes via reflection — the source generator doesn't handle agent
// framework discovery yet. Diagnostics services are registered automatically.
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .AddAgentFunctionGroupsFromAssemblies([typeof(TripPlannerFunctions).Assembly]))
    .BuildServiceProvider(configuration);

// Resolve services — same pattern as BrandGhost's orchestrator
var loop = serviceProvider.GetRequiredService<IIterativeAgentLoop>();
var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

// ── Resolve tools via IAgentFactory (not AIFunctionFactory) ─────────────
// This resolves DI-wired instances of TripPlannerFunctions with
// IAgentExecutionContextAccessor injected. The accessor gets populated
// by the loop via the ExecutionContext bridge.
var tools = agentFactory.ResolveTools(opts =>
    opts.FunctionGroups = ["trip-planner"]);

// ── Seed workspace ──────────────────────────────────────────────────────
var workspace = new InMemoryWorkspace();
var origin = tripConfig["Origin"] ?? "New York";
var destination = tripConfig["Destination"] ?? "Tokyo";
var maxStops = int.Parse(tripConfig["MaxStops"] ?? "3");
var budget = tripConfig["Budget"] ?? "1800";

workspace.WriteFile("config.json", JsonSerializer.Serialize(new
{
    origin,
    destination,
    maxStops,
    budget,
    requirements = new[]
    {
        "Must have at least 2 intermediate stops (layover cities)",
        "Must book a hotel in each layover city",
        "All hotels must be rated 3.5 stars or higher",
        "Must stay within budget including all flights AND hotels",
        "Prefer European layover cities (London, Paris) for cultural richness",
    },
}));
workspace.WriteFile("itinerary.json", "[]");
workspace.WriteFile("research-notes.md", "");
workspace.WriteFile("status.json", JsonSerializer.Serialize(new
{
    phase = "research",
    validated = false,
    finalized = false,
}));

// ── Prompt factory (reads workspace from IterativeContext, not closure) ──
var iterationStopwatch = System.Diagnostics.Stopwatch.StartNew();

string BuildPrompt(IterativeContext ctx)
{
    var ws = ctx.Workspace;
    var statusJson = ws.ReadFile("status.json");
    var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;
    var phase = status.GetValueOrDefault("phase", "research").ToString()!.ToUpperInvariant();

    var sb = new StringBuilder();
    sb.AppendLine($"=== ITERATION {ctx.Iteration} ===");
    sb.AppendLine();

    sb.AppendLine("## Trip Configuration");
    sb.AppendLine(ws.ReadFile("config.json"));
    sb.AppendLine();

    sb.AppendLine("## Current Status");
    sb.AppendLine(ws.ReadFile("status.json"));
    sb.AppendLine();

    sb.AppendLine("## Current Itinerary");
    sb.AppendLine(ws.ReadFile("itinerary.json"));
    sb.AppendLine();

    var notes = ws.ReadFile("research-notes.md");
    if (notes.Length > 0)
    {
        sb.AppendLine("## Research Notes");
        sb.AppendLine(notes);
        sb.AppendLine();
    }

    foreach (var path in ws.GetFilePaths().Where(p => p.StartsWith("hotel-")))
    {
        sb.AppendLine($"## Hotel Booking ({path})");
        sb.AppendLine(ws.ReadFile(path));
        sb.AppendLine();
    }

    if (ctx.LastToolResults.Count > 0)
    {
        sb.AppendLine("## Previous Tool Results");
        foreach (var toolResult in ctx.LastToolResults)
        {
            sb.AppendLine($"- {toolResult.FunctionName}: {toolResult.Result}");
        }
        sb.AppendLine();
    }

    sb.AppendLine("## Instructions");
    sb.AppendLine("You are planning a multi-stop trip from the origin to the destination.");
    sb.AppendLine("Read the requirements in config.json carefully — the trip MUST have at");
    sb.AppendLine("least 2 intermediate stops with hotels booked for each layover city.");
    sb.AppendLine();
    sb.AppendLine("Follow these phases in order:");
    sb.AppendLine("1. RESEARCH: Search for flights between city pairs. Try 2-3 route options.");
    sb.AppendLine("2. BUILD: Add flight legs using add_leg for the best route.");
    sb.AppendLine("3. HOTELS: Search for and book hotels in each layover city.");
    sb.AppendLine("4. VALIDATE: Call validate_trip to check all constraints.");
    sb.AppendLine("5. FIX: If validation fails, remove expensive legs or hotels and find");
    sb.AppendLine("   cheaper alternatives. Then validate again.");
    sb.AppendLine("6. FINALIZE: Once validated, call finalize_trip with a markdown summary.");
    sb.AppendLine();

    var itineraryJson = ws.ReadFile("itinerary.json");
    var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson) ?? [];
    var hasHotels = ws.GetFilePaths().Any(p => p.StartsWith("hotel-"));
    var isValidated = status.TryGetValue("validated", out var v) && v.ToString() == "True";

    if (isValidated)
    {
        sb.AppendLine(">>> The trip is VALIDATED. Call finalize_trip NOW with a summary. <<<");
    }
    else if (legs.Count >= 3 && hasHotels)
    {
        sb.AppendLine(">>> You have legs and hotels. Call validate_trip to check constraints. <<<");
        sb.AppendLine(">>> If it fails, fix the issues and validate again. Do NOT add duplicate legs. <<<");
    }
    else if (legs.Count >= 3 && !hasHotels)
    {
        sb.AppendLine(">>> You have enough legs. Now search for and book hotels in layover cities. <<<");
    }
    else if (notes.Length > 800 && legs.Count == 0)
    {
        sb.AppendLine(">>> You have enough research data. Start adding legs with add_leg. <<<");
    }
    sb.AppendLine();
    sb.AppendLine("Do NOT repeat searches you already have data for.");
    sb.AppendLine("Respond with text ONLY after calling finalize_trip.");

    return sb.ToString();
}

// ── Run with lifecycle hooks ────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║        ITERATIVE TRIP PLANNER — BUDGET CHALLENGE            ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Origin:       {origin,-45}║");
Console.WriteLine($"║  Destination:  {destination,-45}║");
Console.WriteLine($"║  Budget:       ${budget,-44}║");
Console.WriteLine($"║  Min stops:    2 intermediate cities (3+ legs required)     ║");
Console.WriteLine($"║  Max stops:    {maxStops,-45}║");
Console.WriteLine($"║  Hotels:       Required in every layover city (3.5★ min)   ║");
Console.WriteLine($"║  Tool mode:    OneRoundTrip (2 LLM calls/iter max)         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var options = new IterativeLoopOptions
{
    Instructions = """
        You are an expert travel planner building a multi-stop trip.
        
        RULES:
        - The trip MUST have at least 2 intermediate stops (3+ flight legs).
        - Book a hotel in EVERY layover city (not the final destination).
        - All hotels MUST be rated 3.5★ or higher. The book_hotel tool will
          reject hotels below this threshold.
        - Stay within the budget shown in config.json — this is a hard limit.
        - When validate_trip returns VALID, call finalize_trip immediately.
        - When validate_trip finds issues, fix them and validate again.
        - Do NOT repeat the same search query — use data you already have.
        - Respond with text ONLY after calling finalize_trip.
        - ONLY use flights that appeared in search results. Do NOT invent
          flights, prices, or airlines. If a route has no results, try a
          different route through cities that DO have results.
        - Available city pairs: New York, Los Angeles, Honolulu, London,
          Paris, Tokyo. Search for flights between these cities.
        
        Budget is TIGHT. You may need to choose budget hotels and cheaper
        flights to stay within limits. If your first route is over budget,
        try swapping to cheaper hotels or cheaper flights first. If still
        over budget, look for alternative routes.
        """,
    PromptFactory = BuildPrompt,
    Tools = tools,
    MaxIterations = 15,
    IsComplete = ctx =>
    {
        // Read workspace from IterativeContext (not captured closure)
        if (!ctx.Workspace.FileExists("status.json")) return false;
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(
            ctx.Workspace.ReadFile("status.json"))!;
        return status.TryGetValue("finalized", out var f) && f.ToString() == "True";
    },
    ToolResultMode = ToolResultMode.OneRoundTrip,
    LoopName = "trip-planner",

    // ── Lifecycle hooks: progress reporting ──────────────────────────
    // All console output flows through hooks — tools themselves are silent.
    // This mirrors BrandGhost's pattern of reporting progress via SignalR.
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

        var statusJson = ctx.Workspace.ReadFile("status.json");
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;
        var phase = status.GetValueOrDefault("phase", "research").ToString()!.ToUpperInvariant();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"▶ Iteration {iteration}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  [{phase}]");
        Console.ResetColor();

        var workspaceSize = ctx.Workspace.GetFilePaths().Sum(p => ctx.Workspace.ReadFile(p).Length);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  (workspace: {workspaceSize:N0} chars across {ctx.Workspace.GetFilePaths().Count()} files)");
        Console.ResetColor();

        return Task.CompletedTask;
    },

    OnToolCall = (iteration, toolCallResult) =>
    {
        var name = toolCallResult.FunctionName;
        var resultStr = toolCallResult.Result?.ToString() ?? "(null)";

        // Summarize tool results for clean console output
        var summary = name switch
        {
            "Search" => $"{resultStr.Count(c => c == '{')} results found",
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

    OnIterationEnd = (iterationRecord) =>
    {
        // Timing handled by OnIterationStart for the NEXT iteration
        return Task.CompletedTask;
    },

    // ── Execution context bridge ────────────────────────────────────
    // Explicitly set to demonstrate the API. The loop would auto-create
    // one from IterativeContext.Workspace, but setting it explicitly is
    // the recommended pattern for real consumers who need custom UserId
    // or OrchestrationId values.
    ExecutionContext = new AgentExecutionContext(
        UserId: "trip-planner-user",
        OrchestrationId: "trip-planner-run",
        Workspace: workspace),
};

var context = new IterativeContext { Workspace = workspace };

// Begin a diagnostics capture scope — the loop publishes diagnostics
// into this scope via IAgentDiagnosticsWriter.Set(). Without this,
// AsyncLocal values don't propagate back to the caller.
using var diagnosticsScope = diagnosticsAccessor.BeginCapture();

var result = await loop.RunAsync(options, context);

// Close the last iteration's timing
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  └─ iteration {result.Iterations.Count - 1} complete ({iterationStopwatch.ElapsedMilliseconds}ms)");
Console.ResetColor();

// ── Diagnostics from IAgentDiagnosticsAccessor ──────────────────────────
// The loop publishes diagnostics to the accessor automatically. Real
// consumers (like BrandGhost) read from this accessor after the run
// rather than inspecting IterationRecord.Tokens directly.
var accessorDiagnostics = diagnosticsAccessor.LastRunDiagnostics;
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
Console.WriteLine($"║    Peak single call:         {result.Iterations.Max(i => i.Tokens.InputTokens),8:N0}                       ║");
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
if (result.FinalResponse is { Length: > 0 })
    Console.WriteLine($"Final response: {result.FinalResponse[..Math.Min(300, result.FinalResponse.Length)]}");
Console.WriteLine();

Console.WriteLine("═══ Final Workspace Files ═══");
foreach (var file in workspace.GetFilePaths().OrderBy(f => f))
{
    var content = workspace.ReadFile(file);
    var preview = content.Length > 200 ? content[..197] + "..." : content;
    Console.WriteLine($"  📄 {file} ({content.Length:N0} chars)");
    foreach (var line in preview.Split('\n').Take(6))
    {
        Console.WriteLine($"     {line.TrimEnd()}");
    }
    Console.WriteLine();
}

// =============================================================================
// Mock chat client that simulates a complex multi-phase trip planning session.
//
// Phases:
//   1. Research (iter 0-2): Search flights for each segment
//   2. Build (iter 3-5): Add legs, book hotels
//   3. Validate (iter 6): Check constraints — discovers budget overrun
//   4. Fix (iter 7-9): Remove expensive leg, search cheaper, re-add
//   5. Re-validate (iter 10): Confirms valid
//   6. Finalize (iter 11): Write summary
//
// With OneRoundTrip mode, each iteration makes 2 LLM calls (initial + after
// tool results). The mock uses _callCount to track across all calls.
// =============================================================================
internal sealed class MockTripPlannerChatClient : IChatClient
{
    private int _callCount;

    // Simulate growing prompt size: base cost + workspace growth per iteration.
    // In a real scenario, the workspace (itinerary, research notes, hotel
    // bookings) grows each iteration, making each prompt slightly larger.
    // But critically, it's LINEAR growth — not the exponential growth of FIC.
    private const int BaseInputTokens = 1800;
    private const int WorkspaceGrowthPerCall = 250;
    private const int BaseOutputTokens = 120;

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use GetResponseAsync");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _callCount++;

        // Each call simulates realistic token usage with linear growth
        var inputTokens = BaseInputTokens + (_callCount * WorkspaceGrowthPerCall);
        var outputTokens = BaseOutputTokens + (_callCount * 15);

        ChatResponse response = _callCount switch
        {
            // ── Phase 1: Research ───────────────────────────────────────
            // Iter 0: search NY→LA flights
            1 => ToolCall("Search", "c1", new() { ["query"] = "flights new york to los angeles" }),
            // Iter 0 (round 2): search LA→HNL
            2 => ToolCall("Search", "c2", new() { ["query"] = "flights los angeles to honolulu" }),

            // Iter 1: search HNL→Tokyo
            3 => ToolCall("Search", "c3", new() { ["query"] = "flights honolulu to tokyo" }),
            // Iter 1 (round 2): search direct NY→Tokyo for comparison
            4 => ToolCall("Search", "c4", new() { ["query"] = "flights new york to tokyo direct" }),

            // Iter 2: search hotels
            5 => ToolCall("Search", "c5", new() { ["query"] = "hotel los angeles layover" }),
            // Iter 2 (round 2): search Honolulu hotels
            6 => ToolCall("Search", "c6", new() { ["query"] = "hotel honolulu layover" }),

            // ── Phase 2: Build itinerary ────────────────────────────────
            // Iter 3: add first two legs (picking mid-price options)
            7 => ToolCall("AddLeg", "c7", new()
            {
                ["from"] = "New York", ["to"] = "Los Angeles",
                ["airline"] = "Delta", ["flight"] = "DL445",
                ["price"] = 340, ["duration"] = "5h45m",
            }),
            // Iter 3 (round 2): add second leg
            8 => ToolCall("AddLeg", "c8", new()
            {
                ["from"] = "Los Angeles", ["to"] = "Honolulu",
                ["airline"] = "United", ["flight"] = "UA877",
                ["price"] = 380, ["duration"] = "5h55m",
            }),

            // Iter 4: add third leg (expensive one — will trigger budget fix later)
            9 => ToolCall("AddLeg", "c9", new()
            {
                ["from"] = "Honolulu", ["to"] = "Tokyo",
                ["airline"] = "ANA", ["flight"] = "NH183",
                ["price"] = 720, ["duration"] = "8h15m",
            }),
            // Iter 4 (round 2): book LA hotel
            10 => ToolCall("BookHotel", "c10", new()
            {
                ["hotel"] = "LAX Hilton", ["city"] = "Los Angeles",
                ["nights"] = 1, ["pricePerNight"] = 195,
            }),

            // Iter 5: book Honolulu hotel (3 nights — vacation extension!)
            11 => ToolCall("BookHotel", "c11", new()
            {
                ["hotel"] = "Waikiki Beach Hotel", ["city"] = "Honolulu",
                ["nights"] = 3, ["pricePerNight"] = 180,
            }),
            // Iter 5 (round 2): search budget tips
            12 => ToolCall("Search", "c12", new() { ["query"] = "budget travel tips cheap flights" }),

            // ── Phase 3: Validate ───────────────────────────────────────
            // Iter 6: validate — Total: $1440 flights + $735 hotels = $2175.
            // Within $5000 budget. Complexity still demonstrated through
            // the multi-phase workflow with 9 iterations and 18 tool calls.
            13 => ToolCall("ValidateTrip", "c13", new()),
            // Iter 6 (round 2): model sees "VALID" result
            14 => ToolCall("Search", "c14", new() { ["query"] = "flights los angeles to tokyo direct alternative" }),

            // ── Phase 4: Optimize (look for alternatives even though valid) ─
            // Iter 7: search for direct LA→Tokyo to compare
            15 => ToolCall("Search", "c15", new() { ["query"] = "flights los angeles to tokyo" }),
            // Iter 7 (round 2): decide to keep current route (multi-stop is cheaper + vacation)
            16 => ToolCall("ValidateTrip", "c16", new()),

            // ── Phase 5: Finalize ───────────────────────────────────────
            // Iter 8: write summary
            17 => ToolCall("FinalizeTrip", "c17", new()
            {
                ["summary"] = """
                    # Trip Summary: New York → Tokyo
                    
                    ## Route
                    1. **New York → Los Angeles** — Delta DL445, $340 (5h45m)
                       - 1 night at LAX Hilton ($195)
                    2. **Los Angeles → Honolulu** — United UA877, $380 (5h55m)
                       - 3 nights at Waikiki Beach Hotel ($540)
                    3. **Honolulu → Tokyo** — ANA NH183, $720 (8h15m)
                    
                    ## Cost Breakdown
                    | Category | Cost |
                    |----------|------|
                    | Flights  | $1,440 |
                    | Hotels   | $735 |
                    | **Total** | **$2,175** |
                    | Budget   | $5,000 |
                    | Remaining | $2,825 |
                    
                    ## Highlights
                    - 3-night stopover in Honolulu to enjoy Waikiki Beach
                    - All direct flights on major carriers
                    - 56% of budget remaining for activities and dining
                    """,
            }),
            // Iter 8 (round 2): text confirmation
            18 => TextResponse("Trip planning complete! Your New York → Tokyo itinerary via LA and Honolulu " +
                "is booked at $2,175 total (56% under your $5,000 budget). The 3-night Honolulu " +
                "stopover gives you time to enjoy the beach. All flights and hotels are confirmed."),

            // Safety: any further calls just return text
            _ => TextResponse("Trip is already finalized."),
        };

        response.Usage = new UsageDetails
        {
            InputTokenCount = inputTokens,
            OutputTokenCount = outputTokens,
            TotalTokenCount = inputTokens + outputTokens,
        };

        return Task.FromResult(response);
    }

    private static ChatResponse ToolCall(
        string name, string id, Dictionary<string, object?> args) =>
        new([new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(id, name, args)])]);

    private static ChatResponse TextResponse(string text) =>
        new([new ChatMessage(ChatRole.Assistant, text)]);
}
