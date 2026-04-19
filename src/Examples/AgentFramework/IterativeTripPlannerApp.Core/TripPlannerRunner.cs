using System.Text;
using System.Text.Json;

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

namespace IterativeTripPlannerApp.Core;

/// <summary>
/// Runs the iterative trip planner scenario end-to-end. Encapsulates DI setup,
/// workspace seeding, prompt construction, and loop execution so consumers
/// provide only an <see cref="IChatClient"/> and a <see cref="TripPlannerConfig"/>.
/// </summary>
public sealed class TripPlannerRunner
{
    private readonly IChatClient _chatClient;
    private readonly IReadOnlyList<AITool> _additionalTools;

    /// <summary>
    /// Creates a new runner.
    /// </summary>
    /// <param name="chatClient">The LLM chat client to use for the agent loop.</param>
    /// <param name="additionalTools">
    /// Extra tools (e.g. web search) to offer alongside the trip planner's
    /// built-in domain tools. Pass an empty list if none are needed.
    /// </param>
    public TripPlannerRunner(
        IChatClient chatClient,
        IEnumerable<AITool>? additionalTools = null)
    {
        _chatClient = chatClient;
        _additionalTools = additionalTools?.ToList() ?? [];
    }

    /// <summary>
    /// Runs the trip planner and returns the result.
    /// </summary>
    public async Task<TripPlannerRunResult> RunAsync(
        TripPlannerConfig config,
        TripPlannerHooks? hooks,
        CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder().Build();

        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies([typeof(TripPlannerRunner).Assembly])
            .UsingAgentFramework(af => af
                .UsingChatClient(_chatClient))
            .BuildServiceProvider(configuration);

        var loop = serviceProvider.GetRequiredService<IIterativeAgentLoop>();
        var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
        var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

        var tools = agentFactory.ResolveTools(opts =>
            opts.FunctionGroups = ["trip-planner"]);
        var allTools = tools.Concat(_additionalTools).ToList();

        var workspace = new InMemoryWorkspace();
        SeedWorkspace(workspace, config);

        var options = BuildLoopOptions(config, allTools, workspace, hooks);
        var context = new IterativeContext { Workspace = workspace };

        using var diagnosticsScope = diagnosticsAccessor.BeginCapture();

        var result = await loop.RunAsync(options, context, cancellationToken);

        return new TripPlannerRunResult(
            result,
            diagnosticsAccessor.LastRunDiagnostics,
            workspace);
    }

    private static void SeedWorkspace(InMemoryWorkspace workspace, TripPlannerConfig config)
    {
        workspace.SeedFile("config.json", JsonSerializer.Serialize(new
        {
            origin = config.Origin,
            destination = config.Destination,
            maxStops = config.MaxStops,
            minStops = config.MinStops,
            budget = config.Budget,
            requirements = new[]
            {
                $"Must have at least {config.MinStops} intermediate stops (layover cities)",
                "Must book a hotel in each layover city (1 night minimum)",
                "All hotels must be rated 3.5 stars or higher",
                "Must stay within budget including all flights AND hotels",
                "Use web_search to research real flights and hotel prices",
            },
        }));
        workspace.SeedFile("itinerary.json", "[]");
        workspace.SeedFile("status.json", JsonSerializer.Serialize(new
        {
            phase = "research",
            validated = false,
            finalized = false,
        }));
    }

    private static IterativeLoopOptions BuildLoopOptions(
        TripPlannerConfig config,
        IReadOnlyList<AITool> allTools,
        InMemoryWorkspace workspace,
        TripPlannerHooks? hooks)
    {
        return new IterativeLoopOptions
        {
            Instructions = BuildInstructions(config),
            PromptFactory = ctx => BuildPrompt(ctx, config),
            Tools = allTools,
            MaxIterations = 20,
            IsComplete = ctx =>
            {
                if (!ctx.Workspace.FileExists("status.json")) return false;
                var status = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    ctx.Workspace.TryReadFile("status.json").Value.Content)!;
                return status.TryGetValue("finalized", out var f) && f.ToString() == "True";
            },
            ToolResultMode = ToolResultMode.OneRoundTrip,
            LoopName = "trip-planner",
            ToolFilter = (iteration, ctx, tools) =>
            {
                var statusJson = ctx.Workspace.TryReadFile("status.json").Value.Content;
                var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;
                var validated = status.TryGetValue("validated", out var v) && v.ToString() == "True";
                return validated
                    ? tools
                    : tools.Where(t => !t.Name.Equals("finalize_trip", StringComparison.OrdinalIgnoreCase)).ToList();
            },
            OnIterationStart = hooks?.OnIterationStart is not null
                ? (iteration, ctx) => hooks.OnIterationStart(iteration, ctx)
                : null,
            OnToolCall = (iteration, toolCallResult) =>
            {
                AutoPersistWebSearch(workspace, toolCallResult);
                return hooks?.OnToolCall?.Invoke(iteration, toolCallResult) ?? Task.CompletedTask;
            },
            OnIterationEnd = hooks?.OnIterationEnd is not null
                ? (iterationRecord) => hooks.OnIterationEnd(iterationRecord)
                : null,
            ExecutionContext = new AgentExecutionContext(
                UserId: "trip-planner-user",
                OrchestrationId: "trip-planner-run",
                Workspace: workspace),
        };
    }

    private static string BuildInstructions(TripPlannerConfig config) =>
        $"""
        You are an expert travel planner building a multi-stop trip.
        
        RULES:
        - The trip MUST have at least {config.MinStops} intermediate stops ({config.MinStops + 1}+ flight legs).
        - Book a hotel in EVERY layover city (not the final destination).
        - All hotels MUST be rated 3.5★ or higher. The book_hotel tool will
          reject hotels below this threshold.
        - Stay within the budget shown in config.json — this is a hard limit.
        - Use web_search for ALL research — finding flights, comparing prices,
          discovering hotels. This is your ONLY research tool.
        - After EVERY web_search call, immediately call save_research with the
          key facts (airline, flight, price, duration for flights; hotel name,
          price per night, rating for hotels). This persists your findings.
        - When adding a leg with add_leg, use SIMPLE city names like 'New York',
          'Los Angeles', 'Tokyo' — NOT airport codes or parenthesized forms like
          'New York (JFK)'.
        - When adding a leg with add_leg, use realistic data from your web search
          results (airline, flight number, approximate price, duration).
        - When booking a hotel with book_hotel, provide the hotel name, city,
          nights, price per night, and star rating from your web search results.
        - When validate_trip returns VALID, call finalize_trip immediately.
        - When validate_trip finds issues, fix them and validate again.
        - Respond with text ONLY after calling finalize_trip.
        
        Budget is TIGHT. You may need to choose budget hotels and cheaper
        flights to stay within limits. If your first route is over budget,
        try swapping to cheaper hotels or cheaper flights first. If still
        over budget, look for alternative routes via clear_itinerary.
        """;

    private static string BuildPrompt(IterativeContext ctx, TripPlannerConfig config)
    {
        var ws = ctx.Workspace;
        var statusJson = ws.TryReadFile("status.json").Value.Content;
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;

        var sb = new StringBuilder();
        sb.AppendLine($"=== ITERATION {ctx.Iteration} ===");
        sb.AppendLine();

        sb.AppendLine("## Trip Configuration");
        sb.AppendLine(ws.TryReadFile("config.json").Value.Content);
        sb.AppendLine();

        sb.AppendLine("## Current Status");
        sb.AppendLine(ws.TryReadFile("status.json").Value.Content);
        sb.AppendLine();

        sb.AppendLine("## Current Itinerary");
        sb.AppendLine(ws.TryReadFile("itinerary.json").Value.Content);
        sb.AppendLine();

        if (ws.FileExists("research-notes.md"))
        {
            var notes = ws.TryReadFile("research-notes.md").Value.Content;
            if (notes.Length > 0)
            {
                sb.AppendLine("## Research Notes (from previous web searches)");
                sb.AppendLine(notes);
                sb.AppendLine();
            }
        }

        foreach (var path in ws.GetFilePaths().Where(p => p.StartsWith("hotel-")))
        {
            var content = ws.TryReadFile(path).Value.Content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                sb.AppendLine($"## Hotel Booking ({path})");
                sb.AppendLine(content);
                sb.AppendLine();
            }
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
        sb.AppendLine("Read the requirements in config.json carefully.");
        sb.AppendLine();
        sb.AppendLine("Use web_search for ALL research — finding flights, comparing prices,");
        sb.AppendLine("discovering hotels. There is no other search tool.");
        sb.AppendLine("IMPORTANT: After each web_search, call save_research with the key facts");
        sb.AppendLine("(route, airline, price, duration) so you don't lose the data between iterations.");
        sb.AppendLine();
        sb.AppendLine("Follow these phases in order:");
        sb.AppendLine("1. RESEARCH: Use web_search to find flights between city pairs.");
        sb.AppendLine("2. BUILD: Add flight legs using add_leg with data from your research.");
        sb.AppendLine("3. HOTELS: Use web_search to find hotels, then call book_hotel.");
        sb.AppendLine("4. VALIDATE: Call validate_trip to check all constraints.");
        sb.AppendLine("5. FIX: If validation fails, adjust legs/hotels and validate again.");
        sb.AppendLine("6. FINALIZE: Once validated, call finalize_trip with a markdown summary.");
        sb.AppendLine();

        var itineraryJson = ws.TryReadFile("itinerary.json").Value.Content;
        var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson) ?? [];
        var hasHotels = ws.GetFilePaths().Any(p =>
            p.StartsWith("hotel-") && !string.IsNullOrWhiteSpace(ws.TryReadFile(p).Value.Content));
        var isValidated = status.TryGetValue("validated", out var v) && v.ToString() == "True";
        var reqMinStops = config.MinStops;

        if (isValidated)
        {
            sb.AppendLine(">>> The trip is VALIDATED. Call finalize_trip NOW with a summary. <<<");
        }
        else if (legs.Count >= reqMinStops + 1 && hasHotels)
        {
            sb.AppendLine(">>> You have legs and hotels. Call validate_trip to check constraints. <<<");
        }
        else if (legs.Count >= reqMinStops + 1 && !hasHotels)
        {
            sb.AppendLine(">>> You have enough legs. Now use web_search to find hotels in layover cities. <<<");
        }
        sb.AppendLine();
        sb.AppendLine("Respond with text ONLY after calling finalize_trip.");

        return sb.ToString();
    }

    /// <summary>
    /// Auto-persists web_search results to the workspace so they survive between
    /// iterations, regardless of whether the LLM calls SaveResearch on its own.
    /// </summary>
    internal static void AutoPersistWebSearch(IWorkspace workspace, ToolCallResult toolCallResult)
    {
        var name = toolCallResult.FunctionName;
        var resultStr = NexusLabs.Needlr.AgentFramework.ToolResultSerializer.Serialize(toolCallResult.Result);

        if (name != "web_search" || resultStr.Length <= 0)
        {
            return;
        }

        var existing = workspace.FileExists("research-notes.md")
            ? workspace.TryReadFile("research-notes.md").Value.Content
            : "";
        var query = toolCallResult.Arguments.TryGetValue("query", out var q)
            ? q?.ToString() ?? "unknown"
            : "unknown";
        var entry = $"\n### {query}\n{(resultStr.Length > 500 ? resultStr[..500] : resultStr)}\n";
        var updated = existing + entry;
        if (updated.Length > 3000)
            updated = updated[^3000..];
        workspace.TryWriteFile("research-notes.md", updated);
    }
}
