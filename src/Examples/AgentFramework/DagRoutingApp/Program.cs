// =============================================================================
// DAG Routing Modes Example
// =============================================================================
// Demonstrates three DAG routing modes side-by-side using Needlr's graph
// workflow attributes and IGraphWorkflowRunner.
//
// Scenario 1: FirstMatching — edges evaluated in declaration order; only the
//   first whose condition returns true is followed. Remaining edges are skipped.
//   Graph: TriageAgent → UrgentHandler | RoutineHandler | FallbackHandler
//
// Scenario 2: ExclusiveChoice — exactly one edge must match. Zero or multiple
//   matches cause a runtime error, enforcing strict one-of-N routing.
//   Graph: ClassifierAgent → TechnicalAgent | CreativeAgent
//
// Scenario 3: WaitAny — fan-out to parallel workers; the downstream node
//   fires as soon as the first upstream worker completes, cancelling the rest.
//   Graph: DispatchAgent → FastWorker + SlowWorker → ResultAgent (WaitAny)
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login` first)
//   - No API keys needed — auth flows through your GitHub OAuth token
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Copilot;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Spectre.Console;

// Generated extension methods from source generator in DagRoutingApp.Agents.
using DagRoutingApp.Agents.Generated;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var copilotSection = configuration.GetSection("Copilot");
var copilotOptions = new CopilotChatClientOptions
{
    DefaultModel = copilotSection["Model"] ?? "claude-sonnet-4",
};
IChatClient chatClient = new CopilotChatClient(copilotOptions);

var serviceProvider = new Syringe()
    .UsingSourceGen()
    .UsingGeneratedComponents(
        DagRoutingApp.Generated.TypeRegistry.GetInjectableTypes,
        DagRoutingApp.Generated.TypeRegistry.GetPluginTypes)
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .UsingDiagnostics())
    .UsingGraphWorkflows()
    .BuildServiceProvider(configuration);

var graphRunner = serviceProvider.GetRequiredService<IGraphWorkflowRunner>();

AnsiConsole.Write(new Rule("[bold cyan]Needlr DAG Routing Modes[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine($"  [dim]LLM:[/]  Copilot ([green]{Markup.Escape(copilotOptions.DefaultModel)}[/])");
AnsiConsole.WriteLine();

// ============================================================================
// Scenario 1: FirstMatching Routing
// ============================================================================

AnsiConsole.Write(new Rule("[bold yellow]Scenario 1: FirstMatching Routing[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine("""
  [dim]Edges are evaluated in declaration order. Only the FIRST edge whose
  condition returns true is followed. Remaining edges are skipped entirely.
  An unconditional edge (no Condition) acts as a fallback if placed last.[/]
""");

await RunScenario(graphRunner, "priority-routing", "urgent: server is down!",
    "Input contains 'urgent' → expect UrgentHandler");

await RunScenario(graphRunner, "priority-routing", "routine: weekly status update",
    "Input contains 'routine' → expect RoutineHandler");

await RunScenario(graphRunner, "priority-routing", "hello, can you help me?",
    "No keyword match → expect FallbackHandler (unconditional edge)");

// ============================================================================
// Scenario 2: ExclusiveChoice Routing
// ============================================================================

AnsiConsole.Write(new Rule("[bold yellow]Scenario 2: ExclusiveChoice Routing[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine("""
  [dim]Exactly ONE edge condition must match. If zero or more than one match,
  the router throws an error. This enforces strict one-of-N classification.[/]
""");

await RunScenario(graphRunner, "exclusive-routing", "technical: optimize the database queries",
    "Input contains 'technical' → expect TechnicalAgent");

await RunScenario(graphRunner, "exclusive-routing", "creative: design a new logo concept",
    "Input contains 'creative' → expect CreativeAgent");

// ============================================================================
// Scenario 3: WaitAny Join
// ============================================================================

AnsiConsole.Write(new Rule("[bold yellow]Scenario 3: WaitAny Join[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine("""
  [dim]Two workers race in parallel. The ResultAgent (WaitAny) fires as soon as
  the first worker completes. The slower worker's result may appear in
  NodeResults if it finished before the graph terminated, but the join node
  did not wait for it.[/]
""");

await RunScenario(graphRunner, "fast-wins", "Explain dependency injection in one sentence.",
    "FastWorker has minimal instructions (fast) vs SlowWorker (detailed analysis)");

// ============================================================================
// Done
// ============================================================================

AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold green]✓ All scenarios complete[/]").RuleStyle("grey"));
return 0;

// =============================================================================
// Scenario runner — executes a named graph, prints node-level diagnostics
// =============================================================================

static async Task RunScenario(
    IGraphWorkflowRunner graphRunner,
    string graphName,
    string input,
    string expectation)
{
    AnsiConsole.MarkupLine($"\n  [bold aqua]▸ {Markup.Escape(expectation)}[/]");
    AnsiConsole.MarkupLine($"  [bold yellow]Q:[/] {Markup.Escape(input)}");

    IDagRunResult result;
    try
    {
        result = await graphRunner.RunGraphAsync(graphName, input);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [red]ERROR:[/] {Markup.Escape(ex.Message)}");
        AnsiConsole.WriteLine();
        return;
    }

    // Node-level diagnostics table
    var table = new Table()
        .Border(TableBorder.Simple)
        .AddColumn("[bold]Node[/]")
        .AddColumn("[bold]Duration[/]")
        .AddColumn("[bold]Start Offset[/]")
        .AddColumn("[bold]Tokens[/]")
        .AddColumn("[bold]Status[/]");

    foreach (var (nodeId, nodeResult) in result.NodeResults)
    {
        var shortId = ShortName(nodeId);
        var tokens = nodeResult.Diagnostics?.AggregateTokenUsage?.TotalTokens ?? 0;
        var status = nodeResult.FinalResponse is not null
            ? "[green]✓ executed[/]"
            : "[dim]– skipped[/]";

        table.AddRow(
            $"[bold]{Markup.Escape(shortId)}[/]",
            $"{nodeResult.Duration.TotalMilliseconds:F0}ms",
            $"+{nodeResult.StartOffset.TotalMilliseconds:F0}ms",
            $"{tokens}",
            status);
    }

    AnsiConsole.Write(table);

    // Print brief response excerpts from agents that executed
    foreach (var stage in result.Stages)
    {
        var text = stage.FinalResponse?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            continue;
        }

        var preview = text.Length > 150 ? text[..147] + "..." : text;
        AnsiConsole.MarkupLine($"  [green]{Markup.Escape(ShortName(stage.AgentName))}:[/] {Markup.Escape(preview)}");
    }

    // Summary line
    if (result.AggregateTokenUsage is { } agg)
    {
        AnsiConsole.MarkupLine(
            $"  [dim]Total: {agg.TotalTokens} tokens, {result.TotalDuration.TotalSeconds:F1}s[/]");
    }

    AnsiConsole.WriteLine();
}

static string ShortName(string id)
{
    var idx = id.IndexOf('_');
    return idx > 0 ? id[..idx] : id;
}
