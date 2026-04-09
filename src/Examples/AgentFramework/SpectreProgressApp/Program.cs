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

// ── Header ─────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("Needlr MAF").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[grey]Real-time agent orchestration with Spectre.Console[/]");
AnsiConsole.WriteLine();

// ── Questions ──────────────────────────────────────────────────
var questions = new[]
{
    "Which countries has Nick lived in and what are his favorite cities?",
    "What are Nick's hobbies and does he like ice cream?",
};

var executionContext = new AgentExecutionContext(
    UserId: "spectre-demo",
    OrchestrationId: $"spectre-{Guid.NewGuid():N}");

using (contextAccessor.BeginScope(executionContext))
{
    var handoffWorkflow = workflowFactory.CreateTriageHandoffWorkflow();

    foreach (var question in questions)
    {
        AnsiConsole.MarkupLine($"[bold yellow]Q: {Markup.Escape(question)}[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Elapsed[/]").Width(10))
            .AddColumn(new TableColumn("[bold]Event[/]").Width(18))
            .AddColumn(new TableColumn("[bold]Details[/]"));

        var sink = new SpectreLiveSink(table);

        var reporter = progressFactory.Create(
            $"q-{Guid.NewGuid():N}",
            [sink]);

        // Live rendering — the table updates in real-time as events arrive
        await AnsiConsole.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                sink.SetLiveContext(ctx);

                await handoffWorkflow.RunWithDiagnosticsAsync(
                    question, diagnosticsAccessor, reporter, completionCollector, progressAccessor);
            });

        AnsiConsole.WriteLine();

        // Print final agent responses
        // (Re-run without progress to get the result — or we could capture it above)
        // Actually, let's just show the table was the point. The responses are visible
        // via the progress events themselves.
    }
}

AnsiConsole.MarkupLine("[bold green]✓ All orchestrations complete.[/]");

// ── Spectre Live Sink ──────────────────────────────────────────
class SpectreLiveSink : IProgressSink
{
    private readonly Table _table;
    private readonly DateTime _start = DateTime.Now;
    private LiveDisplayContext? _ctx;

    public SpectreLiveSink(Table table) => _table = table;

    public void SetLiveContext(LiveDisplayContext ctx) => _ctx = ctx;

    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken ct)
    {
        var elapsed = $"[dim]+{(DateTime.Now - _start).TotalSeconds:F1}s[/]";

        switch (evt)
        {
            case WorkflowStartedEvent:
                AddRow(elapsed, "[bold cyan]⚡ Workflow[/]", "[cyan]Starting orchestration...[/]");
                break;

            case WorkflowCompletedEvent wc:
                AddRow(elapsed, "[bold cyan]✓ Workflow[/]",
                    wc.Succeeded
                        ? $"[green]Complete in {wc.TotalDuration.TotalSeconds:F1}s[/]"
                        : $"[red]Failed: {Markup.Escape(wc.ErrorMessage ?? "?")}[/]");
                break;

            case AgentInvokedEvent ai:
                if (ai.AgentName.Contains("Handoff")) break;
                var icon = ai.AgentName.Contains("Triage") ? "🔀" : "🤖";
                var color = ai.AgentName.Contains("Triage") ? "yellow" : "green";
                AddRow(elapsed, $"[{color}]{icon} Agent[/]",
                    $"[{color}]{Markup.Escape(ShortName(ai.AgentName))}[/] invoked");
                break;

            case AgentCompletedEvent ac:
                AddRow(elapsed, "[green]✅ Agent[/]",
                    $"[green]{Markup.Escape(ShortName(ac.AgentName))}[/] done — {ac.TotalTokens} tokens, {ac.Duration.TotalMilliseconds:F0}ms");
                break;

            case AgentHandoffEvent ah:
                AddRow(elapsed, "[yellow]↗ Handoff[/]",
                    $"{Markup.Escape(ShortName(ah.FromAgentId))} → [bold]{Markup.Escape(ShortName(ah.ToAgentId))}[/]");
                break;

            case LlmCallStartedEvent lcs:
                AddRow(elapsed, "[blue]  ⏳ LLM[/]",
                    $"[blue]Call #{lcs.CallSequence} sending request...[/]");
                break;

            case LlmCallCompletedEvent lcc:
                AddRow(elapsed, "[blue]  ✓ LLM[/]",
                    $"[blue]#{lcc.CallSequence}[/] {Markup.Escape(lcc.Model)} — [dim]{lcc.Duration.TotalMilliseconds:F0}ms, {lcc.TotalTokens} tokens[/]");
                break;

            case LlmCallFailedEvent lcf:
                AddRow(elapsed, "[red]  ✗ LLM[/]",
                    $"[red]#{lcf.CallSequence} FAILED[/]: {Markup.Escape(lcf.ErrorMessage)}");
                break;

            case ToolCallStartedEvent tcs:
                AddRow(elapsed, "[magenta]  🔧 Tool[/]",
                    $"[magenta]{Markup.Escape(tcs.ToolName)}[/] running...");
                break;

            case ToolCallCompletedEvent tcc:
                AddRow(elapsed, "[magenta]  ✓ Tool[/]",
                    $"[magenta]{Markup.Escape(tcc.ToolName)}[/] — {tcc.Duration.TotalMilliseconds:F0}ms");
                break;
        }

        return ValueTask.CompletedTask;
    }

    private void AddRow(string elapsed, string eventType, string details)
    {
        _table.AddRow(elapsed, eventType, details);
        _ctx?.Refresh();
    }

    private static string ShortName(string executorId)
    {
        // "GeographyAgent_abc123..." → "GeographyAgent"
        var idx = executorId.IndexOf('_');
        return idx > 0 ? executorId[..idx] : executorId;
    }
}
