using Azure;
using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using SimpleAgentFrameworkApp.Agents.Generated;

using Spectre.Console;

// ── Configuration ──────────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var azureSection = configuration.GetSection("AzureOpenAI");
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(azureSection["Endpoint"]
            ?? throw new InvalidOperationException("No AzureOpenAI:Endpoint set")),
        new AzureKeyCredential(azureSection["ApiKey"]
            ?? throw new InvalidOperationException("No AzureOpenAI:ApiKey set")))
    .GetChatClient(azureSection["DeploymentName"]
        ?? throw new InvalidOperationException("No AzureOpenAI:DeploymentName set"))
    .AsIChatClient();

// ── DI Setup ───────────────────────────────────────────────────
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAssemblyProvider(b => b.MatchingAssemblies(path =>
        path.Contains("SimpleAgentFrameworkApp", StringComparison.OrdinalIgnoreCase)).Build())
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .UsingToolResultMiddleware()
        .UsingResilience()
        .UsingDiagnostics())
    .BuildServiceProvider(configuration);

var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();
var contextAccessor = serviceProvider.GetRequiredService<IAgentExecutionContextAccessor>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();
var completionCollector = serviceProvider.GetRequiredService<IChatCompletionCollector>();
var progressFactory = serviceProvider.GetRequiredService<IProgressReporterFactory>();
var progressAccessor = serviceProvider.GetRequiredService<IProgressReporterAccessor>();

// ── Spectre Console Progress Sink ──────────────────────────────
var spectreTable = new Table()
    .Border(TableBorder.Rounded)
    .Title("[bold cyan]Agent Orchestration Progress[/]")
    .AddColumn(new TableColumn("[bold]Time[/]").Width(10))
    .AddColumn(new TableColumn("[bold]Event[/]").Width(20))
    .AddColumn(new TableColumn("[bold]Details[/]").Width(60));

var sink = new SpectreTableSink(spectreTable);

// ── Run the orchestration ──────────────────────────────────────
var executionContext = new AgentExecutionContext(
    UserId: "spectre-demo",
    OrchestrationId: $"spectre-{Guid.NewGuid():N}");

var questions = new[]
{
    "Which countries has Nick lived in and what are his favorite cities?",
    "What are Nick's hobbies and does he like ice cream?",
};

AnsiConsole.Write(new FigletText("Needlr MAF").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[grey]Real-time progress reporting with Spectre.Console[/]");
AnsiConsole.WriteLine();

using (contextAccessor.BeginScope(executionContext))
{
    var handoffWorkflow = workflowFactory.CreateTriageHandoffWorkflow();

    foreach (var question in questions)
    {
        AnsiConsole.MarkupLine($"[bold yellow]Q: {Markup.Escape(question)}[/]");
        AnsiConsole.WriteLine();

        var reporter = progressFactory.Create(
            $"q-{Guid.NewGuid():N}",
            [sink]);

        var result = await handoffWorkflow.RunWithDiagnosticsAsync(
            question, diagnosticsAccessor, reporter, completionCollector, progressAccessor);

        AnsiConsole.Write(spectreTable);
        spectreTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold cyan]Agent Orchestration Progress[/]")
            .AddColumn(new TableColumn("[bold]Time[/]").Width(10))
            .AddColumn(new TableColumn("[bold]Event[/]").Width(20))
            .AddColumn(new TableColumn("[bold]Details[/]").Width(60));
        sink = new SpectreTableSink(spectreTable);

        // Print agent responses
        foreach (var stage in result.Stages)
        {
            AnsiConsole.MarkupLine($"  [green]{Markup.Escape(stage.AgentName)}[/]: {Markup.Escape(stage.ResponseText.Trim())}");
        }

        if (result.AggregateTokenUsage is { } tokens)
        {
            AnsiConsole.MarkupLine($"  [dim]Total: {tokens.TotalTokens} tokens, {result.TotalDuration.TotalMilliseconds:F0}ms[/]");
        }

        AnsiConsole.WriteLine();
    }
}

AnsiConsole.MarkupLine("[bold green]Done![/]");

// ── Spectre Sink Implementation ────────────────────────────────
class SpectreTableSink : IProgressSink
{
    private readonly Table _table;
    private readonly DateTime _start = DateTime.Now;

    public SpectreTableSink(Table table) => _table = table;

    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken ct)
    {
        var elapsed = $"+{(DateTime.Now - _start).TotalSeconds:F1}s";

        switch (evt)
        {
            case WorkflowStartedEvent:
                _table.AddRow(
                    $"[dim]{elapsed}[/]",
                    "[bold cyan]Workflow[/]",
                    "[cyan]Started[/]");
                break;

            case WorkflowCompletedEvent wc:
                _table.AddRow(
                    $"[dim]{elapsed}[/]",
                    "[bold cyan]Workflow[/]",
                    wc.Succeeded
                        ? $"[green]Completed ({wc.TotalDuration.TotalSeconds:F1}s)[/]"
                        : $"[red]Failed: {Markup.Escape(wc.ErrorMessage ?? "unknown")}[/]");
                break;

            case AgentInvokedEvent ai:
                var agentName = ai.AgentName;
                if (agentName.Contains("Triage"))
                    _table.AddRow($"[dim]{elapsed}[/]", "[yellow]→ Agent[/]", $"[yellow]{Markup.Escape(agentName)}[/] routing...");
                else if (agentName.Contains("Handoff"))
                    break; // skip internal handoff markers
                else
                    _table.AddRow($"[dim]{elapsed}[/]", "[green]→ Agent[/]", $"[green]{Markup.Escape(agentName)}[/] working...");
                break;

            case AgentCompletedEvent ac:
                _table.AddRow(
                    $"[dim]{elapsed}[/]",
                    "[green]✓ Agent[/]",
                    $"[green]{Markup.Escape(ac.AgentName)}[/] done ({ac.TotalTokens} tokens)");
                break;

            case LlmCallStartedEvent lcs:
                _table.AddRow(
                    $"[dim]{elapsed}[/]",
                    "[blue]  ⟳ LLM[/]",
                    $"[blue]Call #{lcs.CallSequence} sending...[/]");
                break;

            case LlmCallCompletedEvent lcc:
                _table.AddRow(
                    $"[dim]{elapsed}[/]",
                    "[blue]  ✓ LLM[/]",
                    $"[blue]#{lcc.CallSequence}[/] {lcc.Model} [dim]{lcc.Duration.TotalMilliseconds:F0}ms[/] {lcc.TotalTokens} tok");
                break;

            case LlmCallFailedEvent lcf:
                _table.AddRow(
                    $"[dim]{elapsed}[/]",
                    "[red]  ✗ LLM[/]",
                    $"[red]#{lcf.CallSequence} FAILED: {Markup.Escape(lcf.ErrorMessage)}[/]");
                break;

            case AgentHandoffEvent ah:
                _table.AddRow(
                    $"[dim]{elapsed}[/]",
                    "[yellow]↗ Handoff[/]",
                    $"{Markup.Escape(ah.FromAgentId)} → [bold]{Markup.Escape(ah.ToAgentId)}[/]");
                break;
        }

        return ValueTask.CompletedTask;
    }
}
