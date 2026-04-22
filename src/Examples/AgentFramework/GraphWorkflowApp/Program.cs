// =============================================================================
// Graph / DAG Workflow Example
// =============================================================================
// Demonstrates Needlr's DAG workflow support: attribute-declared graph topology,
// source-generated factory methods, fan-out/fan-in execution, per-node
// diagnostics, and real-time progress reporting via Spectre.Console.
//
// Topology (declared via attributes on agent classes):
//
//   AnalyzerAgent (entry) ──> WebResearchAgent ──> SummarizerAgent
//                         └──> DatabaseAgent ────┘
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

// Generated extension methods: CreateResearchPipelineGraphWorkflow() on IWorkflowFactory.
// These are emitted by the source generator in GraphWorkflowApp.Agents based on
// [AgentGraphEntry("research-pipeline")] and [AgentGraphEdge] attributes.
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
AnsiConsole.MarkupLine("  [dim]Executor:[/]  RunGraphAsync (auto-selects MAF BSP or Needlr-native)");
AnsiConsole.WriteLine();

// Print the source-generated Mermaid topology diagram.
AnsiConsole.Write(new Rule("[bold yellow]Graph Topology (Mermaid)[/]").RuleStyle("grey"));
AnsiConsole.Write(new Panel(
    Markup.Escape(AgentTopologyGraphDiagnostics.AgentTopologyGraph.Trim()))
    .Border(BoxBorder.Rounded)
    .Header("[dim]source-generated[/]"));
AnsiConsole.WriteLine();

const string question = "What are the key trends in AI agent frameworks for 2025?";
AnsiConsole.MarkupLine($"[bold yellow]Q:[/] {Markup.Escape(question)}");
AnsiConsole.WriteLine();

// Create a progress reporter with a live dashboard sink.
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

        result = await graphRunner.RunGraphAsync(
            "research-pipeline", question, reporter);

        tickCts.Cancel();
        try { await ticker; } catch (OperationCanceledException) { }
        dashboardSink.Refresh();
    });

if (result is null)
{
    AnsiConsole.MarkupLine("[red]DAG result was not captured.[/]");
    return;
}

// Print per-node diagnostics.
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold green]Per-Node Diagnostics[/]").RuleStyle("grey"));

var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[bold]Node[/]")
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
    var status = nodeResult.FinalResponse is not null
        ? "[green]✓[/]"
        : "[red]✗[/]";

    table.AddRow(
        $"[bold]{Markup.Escape(shortId)}[/]",
        $"{nodeResult.Duration.TotalMilliseconds:F0}ms",
        $"{tokens?.TotalTokens ?? 0}",
        $"{llmCalls}",
        $"+{nodeResult.StartOffset.TotalMilliseconds:F0}ms",
        status);
}

AnsiConsole.Write(table);

// Aggregate summary.
if (result.AggregateTokenUsage is { } agg)
{
    AnsiConsole.MarkupLine(
        $"  [dim]Total: {agg.TotalTokens} tokens " +
        $"({agg.InputTokens} in / {agg.OutputTokens} out), " +
        $"{result.TotalDuration.TotalSeconds:F1}s[/]");
}

AnsiConsole.WriteLine();

// Print agent responses.
AnsiConsole.Write(new Rule("[bold cyan]Agent Responses[/]").RuleStyle("grey"));
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
AnsiConsole.MarkupLine("[bold green]✓ Done[/]");

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
                    // A new agent starting means any agent that was streaming
                    // and isn't this one has finished.
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
