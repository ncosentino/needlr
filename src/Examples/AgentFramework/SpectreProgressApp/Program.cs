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
using Spectre.Console.Rendering;

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
AnsiConsole.MarkupLine("[grey]Real-time agent orchestration dashboard[/]");
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
        AnsiConsole.MarkupLine($"\n[bold yellow]Q: {Markup.Escape(question)}[/]\n");

        var dashboard = new DashboardSink();
        var reporter = progressFactory.Create(
            $"q-{Guid.NewGuid():N}",
            [dashboard]);

        IPipelineRunResult? result = null;

        await AnsiConsole.Live(dashboard.Render())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                dashboard.SetContext(ctx);

                result = await handoffWorkflow.RunWithDiagnosticsAsync(
                    question, diagnosticsAccessor, reporter, completionCollector, progressAccessor);
            });

        // Print final responses below the dashboard
        if (result is not null)
        {
            foreach (var stage in result.Stages)
            {
                AnsiConsole.MarkupLine($"  [green]{Markup.Escape(ShortName(stage.AgentName))}[/]: {Markup.Escape(stage.ResponseText.Trim())}");
            }

            if (result.AggregateTokenUsage is { } t)
            {
                AnsiConsole.MarkupLine($"  [dim]{t.TotalTokens} tokens, {result.TotalDuration.TotalSeconds:F1}s[/]");
            }
        }
    }
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold green]✓ All orchestrations complete.[/]");

static string ShortName(string id)
{
    var idx = id.IndexOf('_');
    return idx > 0 ? id[..idx] : id;
}

// ════════════════════════════════════════════════════════════════
// Dashboard — fixed-size display that updates in-place
// ════════════════════════════════════════════════════════════════
class DashboardSink : IProgressSink
{
    private LiveDisplayContext? _ctx;
    private readonly DateTime _start = DateTime.Now;

    // State
    private string _workflowStatus = "[dim]Initializing...[/]";
    private readonly List<AgentState> _agents = [];
    private int _totalTokens;
    private int _llmCallCount;
    private double _totalLlmMs;

    public void SetContext(LiveDisplayContext ctx) => _ctx = ctx;

    public IRenderable Render()
    {
        var elapsed = (DateTime.Now - _start).TotalSeconds;

        var panel = new Table()
            .Border(TableBorder.Heavy)
            .Title("[bold cyan]Orchestration Dashboard[/]")
            .AddColumn(new TableColumn("").Width(80));

        // Status line
        panel.AddRow(new Markup($"  Status: {_workflowStatus}  |  Elapsed: [bold]{elapsed:F1}s[/]  |  LLM calls: [bold]{_llmCallCount}[/]  |  Tokens: [bold]{_totalTokens}[/]"));
        panel.AddEmptyRow();

        // Agent rows
        if (_agents.Count == 0)
        {
            panel.AddRow(new Markup("  [dim]Waiting for agents...[/]"));
        }
        else
        {
            foreach (var agent in _agents)
            {
                panel.AddRow(new Markup(agent.Render()));
            }
        }

        panel.AddEmptyRow();

        // LLM throughput
        var avgMs = _llmCallCount > 0 ? _totalLlmMs / _llmCallCount : 0;
        panel.AddRow(new Markup($"  [dim]Avg LLM latency: {avgMs:F0}ms  |  Throughput: {(_llmCallCount > 0 ? elapsed / _llmCallCount : 0):F1}s/call[/]"));

        return panel;
    }

    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case WorkflowStartedEvent:
                _workflowStatus = "[yellow]Running[/] [yellow]●[/]";
                break;

            case WorkflowCompletedEvent wc:
                _workflowStatus = wc.Succeeded
                    ? $"[green]Complete ✓[/] ({wc.TotalDuration.TotalSeconds:F1}s)"
                    : $"[red]Failed ✗[/]: {Markup.Escape(wc.ErrorMessage ?? "?")}";
                break;

            case AgentInvokedEvent ai:
                if (ai.AgentName.Contains("Handoff")) break;
                var existing = _agents.FirstOrDefault(a => a.Name == ShortName(ai.AgentName));
                if (existing is null)
                {
                    var agent = new AgentState(ShortName(ai.AgentName));
                    agent.Status = "[yellow]⟳ Working...[/]";
                    _agents.Add(agent);
                }
                else
                {
                    existing.Status = "[yellow]⟳ Working...[/]";
                }
                break;

            case AgentCompletedEvent ac:
                var completed = _agents.FirstOrDefault(a => a.Name == ShortName(ac.AgentName));
                if (completed is not null)
                {
                    completed.Status = $"[green]✓ Done[/] ({ac.TotalTokens} tok, {ac.Duration.TotalMilliseconds:F0}ms)";
                    _totalTokens += (int)ac.TotalTokens;
                }
                break;

            case AgentHandoffEvent ah:
                var from = _agents.FirstOrDefault(a => a.Name == ShortName(ah.FromAgentId));
                if (from is not null)
                    from.Status = $"[dim]→ handed off to {ShortName(ah.ToAgentId)}[/]";
                break;

            case LlmCallStartedEvent lcs:
                var callingAgent = _agents.LastOrDefault();
                if (callingAgent is not null)
                {
                    callingAgent.CurrentLlmCall = lcs.CallSequence;
                    callingAgent.LlmStatus = "[blue]⏳ Calling LLM...[/]";
                }
                break;

            case LlmCallCompletedEvent lcc:
                _llmCallCount++;
                _totalLlmMs += lcc.Duration.TotalMilliseconds;
                var respondingAgent = _agents.LastOrDefault();
                if (respondingAgent is not null)
                {
                    respondingAgent.LlmCalls++;
                    respondingAgent.LlmTokens += (int)lcc.TotalTokens;
                    respondingAgent.LlmStatus = $"[blue]✓ #{lcc.CallSequence}[/] {lcc.Duration.TotalMilliseconds:F0}ms";
                }
                break;

            case LlmCallFailedEvent lcf:
                _llmCallCount++;
                _totalLlmMs += lcf.Duration.TotalMilliseconds;
                var failedAgent = _agents.LastOrDefault();
                if (failedAgent is not null)
                    failedAgent.LlmStatus = $"[red]✗ #{lcf.CallSequence} FAILED[/]";
                break;

            case ToolCallStartedEvent tcs:
                var toolAgent = _agents.LastOrDefault();
                if (toolAgent is not null)
                    toolAgent.ToolStatus = $"[magenta]🔧 {Markup.Escape(tcs.ToolName)}...[/]";
                break;

            case ToolCallCompletedEvent tcc:
                var toolDone = _agents.LastOrDefault();
                if (toolDone is not null)
                {
                    toolDone.ToolCalls++;
                    toolDone.ToolStatus = $"[magenta]✓ {Markup.Escape(tcc.ToolName)}[/] {tcc.Duration.TotalMilliseconds:F0}ms";
                }
                break;
        }

        Refresh();
        return ValueTask.CompletedTask;
    }

    private void Refresh()
    {
        if (_ctx is null) return;
        _ctx.UpdateTarget(Render());
        _ctx.Refresh();
    }

    private static string ShortName(string id)
    {
        var idx = id.IndexOf('_');
        return idx > 0 ? id[..idx] : id;
    }

    class AgentState
    {
        public string Name { get; }
        public string Status { get; set; } = "[dim]Pending[/]";
        public string LlmStatus { get; set; } = "";
        public string ToolStatus { get; set; } = "";
        public int CurrentLlmCall { get; set; }
        public int LlmCalls { get; set; }
        public int LlmTokens { get; set; }
        public int ToolCalls { get; set; }

        public AgentState(string name) => Name = name;

        public string Render()
        {
            var line = $"  [bold]{Markup.Escape(Name)}[/]  {Status}";
            if (LlmCalls > 0 || LlmStatus.Length > 0)
                line += $"  |  LLM: {LlmStatus} ({LlmCalls} calls, {LlmTokens} tok)";
            if (ToolCalls > 0 || ToolStatus.Length > 0)
                line += $"  |  Tools: {ToolStatus} ({ToolCalls})";
            return line;
        }
    }
}
