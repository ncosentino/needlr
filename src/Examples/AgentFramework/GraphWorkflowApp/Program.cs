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
    .BuildServiceProvider(configuration);

var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

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

// Create a progress reporter with a simple status sink.
var statusSink = new StatusProgressSink();
var reporter = progressFactory.Create(
    $"dag-{Guid.NewGuid():N}",
    [statusSink]);

IDagRunResult? result = null;
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Starting DAG workflow...", async ctx =>
    {
        statusSink.SetStatusContext(ctx);
        result = await workflowFactory.RunGraphAsync(
            "research-pipeline", question, reporter, diagnosticsAccessor);

        ctx.Status("Complete");
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
// Minimal progress sink that updates Spectre.Console Status text.
// ---------------------------------------------------------------------------
sealed class StatusProgressSink : IProgressSink
{
    private StatusContext? _ctx;
    private readonly List<string> _activeNodes = [];

    internal void SetStatusContext(StatusContext ctx) => _ctx = ctx;

    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case AgentInvokedEvent ai:
                var shortName = ShortName(ai.AgentName);
                if (!_activeNodes.Contains(shortName))
                {
                    _activeNodes.Add(shortName);
                }
                UpdateStatus();
                break;

            case AgentCompletedEvent ac:
                _activeNodes.Remove(ShortName(ac.AgentName));
                UpdateStatus();
                break;

            case AgentFailedEvent af:
                _activeNodes.Remove(ShortName(af.AgentName));
                _ctx?.Status($"[red]✗ {Markup.Escape(ShortName(af.AgentName))} failed[/]");
                break;

            case WorkflowCompletedEvent:
                _activeNodes.Clear();
                _ctx?.Status("[green]✓ All nodes complete[/]");
                break;
        }

        return ValueTask.CompletedTask;
    }

    private void UpdateStatus()
    {
        if (_ctx is null || _activeNodes.Count == 0)
        {
            _ctx?.Status("[dim]Waiting...[/]");
            return;
        }

        var names = string.Join(" + ", _activeNodes.Select(Markup.Escape));
        _ctx.Status($"Running [yellow]{names}[/]...");
    }

    private static string ShortName(string id)
    {
        var idx = id.IndexOf('_');
        return idx > 0 ? id[..idx] : id;
    }
}
