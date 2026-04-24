// =============================================================================
// Graph / DAG Workflow Example
// =============================================================================
// Demonstrates Needlr's DAG workflow support: attribute-declared graph topology,
// source-generated factory methods, fan-out/fan-in execution, per-node
// diagnostics, and real-time progress reporting via Spectre.Console.
//
// Features showcased:
//   1. Conditional routing  — WebResearchAgent branch only runs when the input
//      mentions "web" (AnalyzerAgent.NeedsWebResearch predicate).
//   2. Reducer node         — ResearchReducer merges branch outputs without
//      LLM cost; visible as NodeKind.Reducer in diagnostics.
//   3. Optional edge        — DatabaseAgent edge is IsRequired=false; if it
//      fails the graph still succeeds (degraded, not failed).
//   4. Generated helpers    — Uses GraphNames.ResearchPipeline constant and
//      RunResearchPipelineGraphWorkflowAsync() instead of magic strings.
//
// Topology (declared via attributes on agent classes):
//
//   AnalyzerAgent (entry) ──[condition: NeedsWebResearch]──> WebResearchAgent ──> SummarizerAgent
//                         └──[optional]──> DatabaseAgent ────────────────────────┘
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
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Copilot;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Spectre.Console;
using Spectre.Console.Rendering;

// Generated extension methods: CreateResearchPipelineGraphWorkflow() on IWorkflowFactory,
// RunResearchPipelineGraphWorkflowAsync() on IGraphWorkflowRunner, and
// GraphNames.ResearchPipeline constant — all emitted by the source generator in
// GraphWorkflowApp.Agents based on [AgentGraphEntry("research-pipeline")] and
// [AgentGraphEdge] attributes.
using GraphWorkflowApp.Agents.Generated;

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

// The [ModuleInitializer] emitted by the source generator in GraphWorkflowApp.Agents
// fires on assembly load and registers all [NeedlrAiAgent] types and their
// [AgentGraphEntry]/[AgentGraphEdge] topology with AgentFrameworkGeneratedBootstrap.
var serviceProvider = new Syringe()
    .UsingSourceGen()
    .UsingGeneratedComponents(
        GraphWorkflowApp.Generated.TypeRegistry.GetInjectableTypes,
        GraphWorkflowApp.Generated.TypeRegistry.GetPluginTypes)
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .UsingDiagnostics())
    .UsingGraphWorkflows()
    .BuildServiceProvider(configuration);

var graphRunner = serviceProvider.GetRequiredService<IGraphWorkflowRunner>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();

AnsiConsole.Write(new Rule("[bold cyan]Needlr DAG Graph Workflow[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine($"  [dim]LLM:[/]       Copilot ([green]{copilotOptions.DefaultModel}[/])");
AnsiConsole.MarkupLine($"  [dim]Graph:[/]     [green]{Markup.Escape(GraphNames.ResearchPipeline)}[/] (source-generated constant)");
AnsiConsole.MarkupLine("  [dim]Executor:[/]  RunResearchPipelineGraphWorkflowAsync (generated helper)");
AnsiConsole.WriteLine();

// -------------------------------------------------------------------------
// Feature 1: Conditional routing
// -------------------------------------------------------------------------
AnsiConsole.Write(new Rule("[bold yellow]Feature 1 — Conditional Routing[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine("  The [green]WebResearchAgent[/] edge has [yellow]Condition = nameof(NeedsWebResearch)[/].");
AnsiConsole.MarkupLine("  It only activates when the input contains the word \"web\".");
AnsiConsole.MarkupLine("  We'll run the graph twice: once WITHOUT and once WITH the trigger word.");
AnsiConsole.WriteLine();

// -------------------------------------------------------------------------
// Feature 2: Reducer node
// -------------------------------------------------------------------------
AnsiConsole.Write(new Rule("[bold yellow]Feature 2 — Reducer Node[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine("  [green]ResearchReducer[/] merges branch outputs deterministically (no LLM).");
AnsiConsole.MarkupLine("  It appears as [yellow]NodeKind.Reducer[/] in per-node diagnostics.");
AnsiConsole.WriteLine();

// -------------------------------------------------------------------------
// Feature 3: Optional edge
// -------------------------------------------------------------------------
AnsiConsole.Write(new Rule("[bold yellow]Feature 3 — Optional Edge (IsRequired=false)[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine("  The [green]DatabaseAgent[/] edge is marked [yellow]IsRequired = false[/].");
AnsiConsole.MarkupLine("  If it fails, the graph degrades gracefully instead of aborting.");
AnsiConsole.WriteLine();

// -------------------------------------------------------------------------
// Feature 4: Generated helpers (GraphNames + RunXxxGraphWorkflowAsync)
// -------------------------------------------------------------------------
AnsiConsole.Write(new Rule("[bold yellow]Feature 4 — Source-Generated Helpers[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine($"  [dim]GraphNames.ResearchPipeline[/] = [green]\"{Markup.Escape(GraphNames.ResearchPipeline)}\"[/]");
AnsiConsole.MarkupLine("  [dim]graphRunner.RunResearchPipelineGraphWorkflowAsync()[/] — no magic strings.");
AnsiConsole.WriteLine();

// Print the source-generated Mermaid topology diagram.
AnsiConsole.Write(new Rule("[bold yellow]Graph Topology (Mermaid)[/]").RuleStyle("grey"));
AnsiConsole.Write(new Panel(
    Markup.Escape(AgentTopologyGraphDiagnostics.AgentTopologyGraph.Trim()))
    .Border(BoxBorder.Rounded)
    .Header("[dim]source-generated[/]"));
AnsiConsole.WriteLine();

// =========================================================================
// Run 1 — WITHOUT "web" in the input → WebResearchAgent should be SKIPPED
// =========================================================================
const string questionNoWeb = "What are the key trends in AI agent frameworks for 2025?";
AnsiConsole.Write(new Rule("[bold magenta]Run 1 — No conditional trigger[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine($"[bold yellow]Q:[/] {Markup.Escape(questionNoWeb)}");
AnsiConsole.MarkupLine("[dim]  (does NOT contain \"web\" → WebResearchAgent edge should be skipped)[/]");
AnsiConsole.WriteLine();

var result1 = await RunGraphWithDashboard(graphRunner, progressFactory, questionNoWeb);
if (result1 is not null)
{
    PrintResults("Run 1", result1);
}

// =========================================================================
// Run 2 — WITH "web" in the input → WebResearchAgent should EXECUTE
// =========================================================================
const string questionWithWeb = "What are the key web trends in AI agent frameworks for 2025?";
AnsiConsole.Write(new Rule("[bold magenta]Run 2 — Conditional trigger present[/]").RuleStyle("grey"));
AnsiConsole.MarkupLine($"[bold yellow]Q:[/] {Markup.Escape(questionWithWeb)}");
AnsiConsole.MarkupLine("[dim]  (contains \"web\" → WebResearchAgent edge should execute)[/]");
AnsiConsole.WriteLine();

// Use the generated helper — no magic string needed.
var result2 = await RunGraphWithDashboard(graphRunner, progressFactory, questionWithWeb);
if (result2 is not null)
{
    PrintResults("Run 2", result2);
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold green]✓ Done — all DAG features demonstrated[/]");

// ---------------------------------------------------------------------------
// Run helper: wraps the live dashboard + ticker boilerplate.
// ---------------------------------------------------------------------------
static async Task<IDagRunResult?> RunGraphWithDashboard(
    IGraphWorkflowRunner runner,
    IProgressReporterFactory progressFactory,
    string input)
{
    var dashboardSink = new DagDashboardSink();
    var reporter = progressFactory.Create(
        $"dag-{Guid.NewGuid():N}",
        [dashboardSink]);

    IDagRunResult? result = null;
    await AnsiConsole.Live(dashboardSink.Render())
        .AutoClear(false)
        .Overflow(VerticalOverflow.Ellipsis)
        .StartAsync(async ctx =>
        {
            dashboardSink.SetContext(ctx);

            using var tickCts = new CancellationTokenSource();
            var ticker = Task.Run(async () =>
            {
                while (!tickCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(200, tickCts.Token).ConfigureAwait(false);
                    dashboardSink.Refresh();
                }
            }, tickCts.Token);

            // Use generated helper — bakes in GraphNames.ResearchPipeline at compile time.
            result = await runner.RunResearchPipelineGraphWorkflowAsync(
                input, reporter);

            tickCts.Cancel();
            try { await ticker; } catch (OperationCanceledException) { }
            dashboardSink.Refresh();
        });

    return result;
}

// ---------------------------------------------------------------------------
// Print per-node diagnostics and feature-specific result details.
// ---------------------------------------------------------------------------
static void PrintResults(string runLabel, IDagRunResult result)
{
    AnsiConsole.WriteLine();

    // -- Per-Node Diagnostics (NodeKind + reducer vs agent) --
    AnsiConsole.Write(new Rule($"[bold green]{runLabel} — Per-Node Diagnostics[/]").RuleStyle("grey"));

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[bold]Node[/]")
        .AddColumn("[bold]Kind[/]")
        .AddColumn("[bold]Duration[/]")
        .AddColumn("[bold]Tokens[/]")
        .AddColumn("[bold]LLM Calls[/]")
        .AddColumn("[bold]Start Offset[/]")
        .AddColumn("[bold]Status[/]");

    foreach (var (nodeId, nodeResult) in result.NodeResults)
    {
        var shortId = ShortName(nodeId);
        var tokens = nodeResult.Diagnostics?.AggregateTokenUsage;
        var llmCalls = nodeResult.Diagnostics?.ChatCompletions.Count ?? 0;
        var kindLabel = nodeResult.Kind == NodeKind.Reducer
            ? "[yellow]Reducer[/]"
            : "[cyan]Agent[/]";
        var status = nodeResult.FinalResponse is not null
            ? "[green]✓[/]"
            : nodeResult.Kind == NodeKind.Reducer
                ? "[yellow]✓ (deterministic)[/]"
                : "[red]✗[/]";

        table.AddRow(
            $"[bold]{Markup.Escape(shortId)}[/]",
            kindLabel,
            $"{nodeResult.Duration.TotalMilliseconds:F0}ms",
            $"{tokens?.TotalTokens ?? 0}",
            $"{llmCalls}",
            $"+{nodeResult.StartOffset.TotalMilliseconds:F0}ms",
            status);
    }

    AnsiConsole.Write(table);

    // -- Conditional routing info --
    AnsiConsole.Write(new Rule($"[bold green]{runLabel} — Conditional Routing[/]").RuleStyle("grey"));

    var executedNodes = result.NodeResults.Keys
        .Select(ShortName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    string[] allExpected = ["AnalyzerAgent", "WebResearchAgent", "DatabaseAgent", "SummarizerAgent"];

    foreach (var name in allExpected)
    {
        if (executedNodes.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            AnsiConsole.MarkupLine($"  [green]✓ {name}[/] — executed");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [dim]⊘ {name}[/] — skipped (condition not met)");
        }
    }

    AnsiConsole.WriteLine();

    // -- Optional edge failure handling --
    AnsiConsole.Write(new Rule($"[bold green]{runLabel} — Optional Edge Resilience[/]").RuleStyle("grey"));

    var failedOptional = result.NodeResults
        .Where(kvp => kvp.Value.FinalResponse is null
            && kvp.Value.Kind == NodeKind.Agent)
        .Select(kvp => ShortName(kvp.Key))
        .ToList();

    if (failedOptional.Count > 0)
    {
        foreach (var nodeName in failedOptional)
        {
            AnsiConsole.MarkupLine(
                $"  [yellow]⚠ {Markup.Escape(nodeName)}[/] — failed/no response, " +
                $"but graph succeeded={result.Succeeded} (IsRequired=false)");
        }
    }
    else
    {
        AnsiConsole.MarkupLine(
            result.Succeeded
                ? "  [green]All nodes completed successfully.[/] Graph succeeded."
                : $"  [red]Graph failed:[/] {Markup.Escape(result.ErrorMessage ?? "unknown")}");
    }

    AnsiConsole.MarkupLine(
        $"  [dim]Overall: Succeeded={result.Succeeded}, " +
        $"Error={result.ErrorMessage ?? "(none)"}[/]");
    AnsiConsole.WriteLine();

    // -- Reducer results --
    AnsiConsole.Write(new Rule($"[bold green]{runLabel} — Reducer Nodes[/]").RuleStyle("grey"));

    var reducerNodes = result.NodeResults
        .Where(kvp => kvp.Value.Kind == NodeKind.Reducer)
        .ToList();

    if (reducerNodes.Count > 0)
    {
        foreach (var (nodeId, nodeResult) in reducerNodes)
        {
            AnsiConsole.MarkupLine(
                $"  [yellow]⚡ {Markup.Escape(ShortName(nodeId))}[/] — " +
                $"merged {nodeResult.InboundEdges.Count} inbound edges, " +
                $"duration={nodeResult.Duration.TotalMilliseconds:F0}ms, " +
                $"0 LLM calls (deterministic)");
        }
    }
    else
    {
        AnsiConsole.MarkupLine("  [dim]No reducer nodes executed in this run.[/]");
    }

    AnsiConsole.WriteLine();

    // -- Aggregate summary --
    if (result.AggregateTokenUsage is { } agg)
    {
        AnsiConsole.MarkupLine(
            $"  [dim]Total: {agg.TotalTokens} tokens " +
            $"({agg.InputTokens} in / {agg.OutputTokens} out), " +
            $"{result.TotalDuration.TotalSeconds:F1}s[/]");
    }

    AnsiConsole.WriteLine();

    // -- Agent responses --
    AnsiConsole.Write(new Rule($"[bold cyan]{runLabel} — Agent Responses[/]").RuleStyle("grey"));
    foreach (var stage in result.Stages)
    {
        var responseText = stage.FinalResponse?.Text?.Trim();
        if (string.IsNullOrEmpty(responseText))
        {
            continue;
        }

        AnsiConsole.MarkupLine($"\n[bold green]{Markup.Escape(ShortName(stage.AgentName))}[/]");
        AnsiConsole.WriteLine(responseText.Length > 500
            ? responseText[..500] + "..."
            : responseText);
    }

    AnsiConsole.WriteLine();
}

static string ShortName(string id)
{
    var idx = id.IndexOf('_');
    return idx > 0 ? id[..idx] : id;
}

// ---------------------------------------------------------------------------
// Live dashboard sink showing per-agent streaming output in real time.
// ---------------------------------------------------------------------------
sealed class DagDashboardSink : IProgressSink
{
    private LiveDisplayContext? _ctx;
    private readonly DateTime _start = DateTime.Now;
    private readonly Dictionary<string, AgentPanel> _agents = new();
    private static readonly string[] _spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private int _spinnerIndex;
    private bool _complete;

    internal void SetContext(LiveDisplayContext ctx) => _ctx = ctx;

    internal void Refresh()
    {
        _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;
        _ctx?.UpdateTarget(Render());
    }

    internal IRenderable Render()
    {
        var elapsed = (DateTime.Now - _start).TotalSeconds;
        var table = new Table()
            .Border(TableBorder.Heavy)
            .Title("[bold cyan]DAG Workflow — Live[/]")
            .AddColumn(new TableColumn("").NoWrap().Width(90));

        var spinner = _complete ? "[green]✓[/]" : $"[yellow]{_spinnerFrames[_spinnerIndex]}[/]";
        table.AddRow(new Markup($"  {spinner} Elapsed: [bold]{elapsed:F1}s[/]  |  Agents: [bold]{_agents.Count}[/]"));
        table.AddEmptyRow();

        if (_agents.Count == 0)
        {
            table.AddRow(new Markup("  [dim]Waiting for agents...[/]"));
        }
        else
        {
            foreach (var (_, agent) in _agents)
            {
                var statusIcon = agent.Done
                    ? "[green]✓[/]"
                    : agent.Failed
                        ? "[red]✗[/]"
                        : $"[yellow]{_spinnerFrames[_spinnerIndex]}[/]";
                var preview = agent.Text.Length > 120
                    ? agent.Text[^120..].Replace("\n", " ")
                    : agent.Text.Replace("\n", " ");
                table.AddRow(new Markup($"  {statusIcon} [bold]{Markup.Escape(agent.ShortName)}[/]"));
                if (!string.IsNullOrEmpty(preview))
                {
                    table.AddRow(new Markup($"    [dim]{Markup.Escape(preview.Trim())}[/]"));
                }
                else if (!agent.Done && !agent.Failed)
                {
                    table.AddRow(new Markup("    [dim italic]generating...[/]"));
                }
            }
        }

        return table;
    }

    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case AgentInvokedEvent ai:
                var name = ShortName(ai.AgentName);
                if (!_agents.ContainsKey(name))
                {
                    foreach (var (_, prev) in _agents)
                    {
                        if (!prev.Done && !prev.Failed && prev.Text.Length > 0)
                        {
                            prev.Done = true;
                        }
                    }
                    _agents[name] = new AgentPanel(name);
                    Refresh();
                }
                break;

            case ReducerNodeInvokedEvent ri:
                var reducerName = ShortName(ri.NodeId);
                if (!_agents.ContainsKey(reducerName))
                {
                    _agents[reducerName] = new AgentPanel(reducerName)
                    {
                        Text = $"[Reducer] Merged {ri.InputBranchCount} branches in {ri.Duration.TotalMilliseconds:F0}ms",
                        Done = true,
                    };
                    Refresh();
                }
                break;

            case AgentResponseChunkEvent chunk:
                var chunkName = ShortName(chunk.AgentName);
                if (!_agents.TryGetValue(chunkName, out var panel))
                {
                    _agents[chunkName] = panel = new AgentPanel(chunkName);
                }
                panel.Text += chunk.Text;
                Refresh();
                break;

            case AgentCompletedEvent ac:
                var doneName = ShortName(ac.AgentName);
                if (_agents.TryGetValue(doneName, out var donePanel))
                {
                    donePanel.Done = true;
                }
                Refresh();
                break;

            case AgentFailedEvent af:
                var failName = ShortName(af.AgentName);
                if (_agents.TryGetValue(failName, out var failPanel))
                {
                    failPanel.Failed = true;
                    failPanel.Text += $" [ERROR: {af.ErrorMessage}]";
                }
                Refresh();
                break;

            case WorkflowCompletedEvent:
                _complete = true;
                Refresh();
                break;
        }

        return ValueTask.CompletedTask;
    }

    private static string ShortName(string id)
    {
        var idx = id.IndexOf('_');
        return idx > 0 ? id[..idx] : id;
    }

    private sealed class AgentPanel(string shortName)
    {
        public string ShortName { get; } = shortName;
        public string Text { get; set; } = "";
        public bool Done { get; set; }
        public bool Failed { get; set; }
    }
}
