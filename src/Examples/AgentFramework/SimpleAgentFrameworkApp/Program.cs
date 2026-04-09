using Azure;
using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Providers;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using SimpleAgentFrameworkApp.Agents;

// Generated extension methods emitted by the source generator in SimpleAgentFrameworkApp.Agents.
// Includes: IWorkflowFactory extensions, IAgentFactory extensions, named constants,
// and AgentFrameworkSyringe group registration extensions.
using SimpleAgentFrameworkApp.Agents.Generated;

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

// The [ModuleInitializer] emitted by the source generator in SimpleAgentFrameworkApp.Agents
// fires on assembly load and registers all [NeedlrAiAgent] types, [AgentFunction] types,
// [AgentFunctionGroup] groups, [AgentHandoffsTo] topology, and [AgentSequenceMember] pipelines
// with AgentFrameworkGeneratedBootstrap. UsingAgentFramework() detects these and auto-populates.
//
// Three paved-path middleware features are wired here:
//   UsingResilience()          — Polly retry/timeout on every agent run (2 retries, 120s timeout).
//                                GeographyAgent overrides this with [AgentResilience(3, 90)].
//   UsingToolResultMiddleware() — Intercepts ToolResult<T,E> returns and unhandled exceptions,
//                                converting them to safe JSON for the LLM.
//   UsingTokenBudget()         — Wraps IChatClient to enforce per-pipeline token budgets via
//                                ITokenBudgetTracker.BeginScope().
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAssemblyProvider(b => b.MatchingAssemblies(path =>
        path.Contains("SimpleAgentFrameworkApp", StringComparison.OrdinalIgnoreCase)).Build())
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .UsingToolResultMiddleware()
        .UsingResilience()
        .UsingTokenBudget()
        .UsingDiagnostics())
    .BuildServiceProvider(configuration);

var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();
var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
var tokenBudgetTracker = serviceProvider.GetRequiredService<ITokenBudgetTracker>();
var contextAccessor = serviceProvider.GetRequiredService<IAgentExecutionContextAccessor>();
var completionCollector = serviceProvider.GetRequiredService<IChatCompletionCollector>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

// Strongly-typed agent creation — generated from [NeedlrAiAgent] declarations.
// No magic strings; renaming the class regenerates these methods automatically.
var triageAgent = agentFactory.CreateTriageAgent();
Console.WriteLine($"Created: {AgentNames.TriageAgent} (ID: {triageAgent.Id})");

// --- Demo 1: Handoff workflow with execution context ---
// CreateTriageHandoffWorkflow() is generated because TriageAgent is decorated with [AgentHandoffsTo].
//
// The execution context is established by the trusted caller (this program) before invoking the
// workflow. Tools read identity from the context — never from LLM-provided parameters.
// GeographyFunctions.GetFavoriteCities() demonstrates this by calling contextAccessor.GetRequired().
var handoffWorkflow = workflowFactory.CreateTriageHandoffWorkflow();

Console.WriteLine("=== Demo 1: Triage → Handoff Multi-Agent Workflow ===");
Console.WriteLine("  Topology declared via [AgentHandoffsTo] on TriageAgent.");
Console.WriteLine("  Middleware: UsingResilience() (global), UsingToolResultMiddleware(), UsingTokenBudget().");
Console.WriteLine("  GeographyAgent has [AgentResilience(maxRetries: 3, timeoutSeconds: 90)].");
Console.WriteLine("  GeographyFunctions.GetCountriesLived() returns ToolResult<T, ToolError>.");
Console.WriteLine("  GeographyFunctions.GetFavoriteCities() reads UserId from IAgentExecutionContextAccessor.");
Console.WriteLine();

// Create a per-orchestration workspace. The framework does NOT auto-register workspaces —
// consumers construct them explicitly and attach wherever they choose. Here we put it in
// the execution context's Properties bag, and NoteFunctions reads it from there.
var workspace = new InMemoryWorkspace();

var executionContext = new AgentExecutionContext(
    UserId: "demo-user-42",
    OrchestrationId: $"demo-{Guid.NewGuid():N}",
    Properties: new Dictionary<string, object> { ["workspace"] = workspace });

var questions = new[]
{
    "Which countries has Nick lived in?",
    "What are Nick's top hobbies?",
};

// Wrap agent execution in a context scope — tools see the UserId and OrchestrationId.
// Use RunWithDiagnosticsAsync to capture per-stage diagnostics from the event stream.
// This works for ALL workflow types (handoff, sequential, group chat) because it collects
// diagnostics at turn boundaries, not via AsyncLocal propagation through MAF internals.
using (contextAccessor.BeginScope(executionContext))
{
    foreach (var question in questions)
    {
        Console.WriteLine($"Q: {question}");

        var result = await handoffWorkflow.RunWithDiagnosticsAsync(
            question, diagnosticsAccessor, null, completionCollector);

        foreach (var stage in result.Stages)
        {
            Console.WriteLine($"  [{stage.AgentName}]: {stage.ResponseText.Trim()}");

            if (stage.Diagnostics is { } diag)
            {
                Console.WriteLine($"    Duration: {diag.TotalDuration.TotalMilliseconds:F0}ms | Tokens: {diag.AggregateTokenUsage.TotalTokens}");

                foreach (var cc in diag.ChatCompletions)
                {
                    Console.WriteLine($"    LLM call #{cc.Sequence}: {cc.Duration.TotalMilliseconds:F0}ms model={cc.Model} {(cc.Succeeded ? "OK" : $"FAIL: {cc.ErrorMessage}")}");
                }

                foreach (var tc in diag.ToolCalls)
                {
                    var metricsInfo = tc.CustomMetrics is { Count: > 0 }
                        ? $" metrics: {string.Join(", ", tc.CustomMetrics.Select(m => $"{m.Key}={m.Value}"))}"
                        : "";
                    Console.WriteLine($"    Tool [{tc.ToolName}]: {tc.Duration.TotalMilliseconds:F0}ms {(tc.Succeeded ? "OK" : $"FAIL: {tc.ErrorMessage}")}{metricsInfo}");
                }
            }
        }

        if (result.AggregateTokenUsage is { } aggTokens)
        {
            Console.WriteLine($"  --- Total: {aggTokens.TotalTokens} tokens, {result.TotalDuration.TotalMilliseconds:F0}ms ---");
        }

        Console.WriteLine();
    }
}

// Print workspace contents — shows files the agent tools wrote during the run.
var workspaceFiles = workspace.GetFilePaths().ToList();
if (workspaceFiles.Count > 0)
{
    Console.WriteLine("=== Workspace Contents (InMemoryWorkspace) ===");
    Console.WriteLine("  NoteFunctions.SaveNote() wrote these files via the IWorkspace from");
    Console.WriteLine("  the execution context's Properties bag — fully opt-in, not forced.");
    Console.WriteLine();
    foreach (var file in workspaceFiles)
    {
        Console.WriteLine($"  [{file}]:");
        Console.WriteLine($"    {workspace.ReadFile(file)}");
    }
    Console.WriteLine();
}

// --- Demo: Tiered Provider Fallback ---
// TieredProviderSelector tries providers in priority order, falling through on
// ProviderUnavailableException. Here PremiumGeoAPI (priority 1) always throws,
// so LocalData (priority 100) handles the request.
Console.WriteLine("=== Tiered Provider Fallback ===");
Console.WriteLine("  PremiumGeoAPI (priority 1) throws ProviderUnavailableException.");
Console.WriteLine("  LocalData (priority 100) succeeds as fallback.");
Console.WriteLine();

var providers = new ITieredProvider<string, IReadOnlyList<string>>[]
{
    new PremiumGeographyProvider(),
    new LocalGeographyProvider(),
};
var selector = new TieredProviderSelector<string, IReadOnlyList<string>>(
    providers, new AlwaysGrantQuotaGate());

var countries = await selector.ExecuteAsync("countries", CancellationToken.None);
Console.WriteLine($"  Result: {string.Join(", ", countries)}");
Console.WriteLine();

// --- Demo 2: Token budget enforcement ---
Console.WriteLine("=== Demo 2: Token Budget Enforcement ===");
Console.WriteLine("  ITokenBudgetTracker.BeginScope(maxTokens) opens an AsyncLocal-scoped budget.");
Console.WriteLine("  TokenBudgetChatMiddleware checks before/after each LLM call.");
Console.WriteLine("  Exceeding the budget throws TokenBudgetExceededException.");
Console.WriteLine();

// First: run with a generous budget — should succeed.
// Uses RunWithDiagnosticsAsync so per-LLM-call timing is correlated with budget usage.
Console.WriteLine("  [Generous budget: 100,000 tokens]");
using (contextAccessor.BeginScope(executionContext))
using (tokenBudgetTracker.BeginScope(maxTokens: 100_000))
{
    var workflow = workflowFactory.CreateTriageHandoffWorkflow();
    var budgetResult = await workflow.RunWithDiagnosticsAsync(
        "What cities does Nick like?", diagnosticsAccessor, null, completionCollector);

    foreach (var stage in budgetResult.Stages)
    {
        Console.WriteLine($"    [{stage.AgentName}]: {stage.ResponseText.Trim()}");
        if (stage.Diagnostics is { } diag)
        {
            foreach (var cc in diag.ChatCompletions)
            {
                Console.WriteLine($"      LLM call #{cc.Sequence}: {cc.Duration.TotalMilliseconds:F0}ms tokens={cc.Tokens.TotalTokens}");
            }
        }
    }

    Console.WriteLine($"    Budget: {tokenBudgetTracker.CurrentTokens} / {tokenBudgetTracker.MaxTokens} tokens used");
    if (budgetResult.AggregateTokenUsage is { } budgetTokens)
    {
        Console.WriteLine($"    Diagnostics: {budgetTokens.TotalTokens} tokens ({budgetTokens.InputTokens} in, {budgetTokens.OutputTokens} out)");
    }
}

Console.WriteLine();

// Second: run with a tiny budget — should throw.
Console.WriteLine("  [Tiny budget: 1 token — expect budget cancellation]");
try
{
    using (contextAccessor.BeginScope(executionContext))
    using (tokenBudgetTracker.BeginScope(maxTokens: 1))
    {
        // Pass the budget's CancellationToken to the workflow so MAF stops
        // when the budget is exceeded. The token is cancelled automatically
        // by TokenBudgetTracker.Record() when tokens exceed the limit.
        var budgetToken = tokenBudgetTracker.BudgetCancellationToken;
        var workflow = workflowFactory.CreateTriageHandoffWorkflow();
        await workflow.RunAsync("What cities does Nick like?", cancellationToken: budgetToken);
    }

    Console.WriteLine("    ERROR: Expected cancellation but did not get one.");
}
catch (OperationCanceledException)
{
    // Note: tokenBudgetTracker.CurrentTokens may be 0 here because the scope
    // was disposed by the using block before this catch runs. The budget WAS
    // exceeded — the cancellation proves it. The pre-call gate (0 >= 1 = false)
    // let the first call through, Record() tracked the tokens, the token was
    // cancelled, and ThrowIfCancellationRequested fired after collection.
    Console.WriteLine($"    Caught: Budget exceeded — workflow cancelled before completion.");
}

Console.WriteLine();

// --- Demo 3: Sequential pipeline workflow with per-stage diagnostics ---
// CreateContentPipelineSequentialWorkflow() is generated because WriterSeqAgent, EditorSeqAgent,
// and PublisherSeqAgent are decorated with [AgentSequenceMember("content-pipeline", 1/2/3)].
// RunWithDiagnosticsAsync() captures per-stage diagnostics at each turn boundary,
// returning an IPipelineRunResult with aggregate token usage.
var sequentialWorkflow = workflowFactory.CreateContentPipelineSequentialWorkflow();

Console.WriteLine("=== Demo 3: Content Pipeline Sequential Workflow + Per-Stage Diagnostics ===");
Console.WriteLine("  Pipeline declared via [AgentSequenceMember] on Writer → Editor → Publisher.");
Console.WriteLine("  RunWithDiagnosticsAsync() captures IAgentRunDiagnostics per agent turn.");
Console.WriteLine();

var topics = new[]
{
    "Why C# source generators improve developer experience",
};

foreach (var topic in topics)
{
    Console.WriteLine($"Topic: {topic}");

    var pipelineResult = await sequentialWorkflow.RunWithDiagnosticsAsync(
        topic, diagnosticsAccessor, null, completionCollector);

    foreach (var stage in pipelineResult.Stages)
    {
        Console.WriteLine($"  [{stage.AgentName}]: {stage.ResponseText.Trim()[..Math.Min(120, stage.ResponseText.Trim().Length)]}...");
        if (stage.Diagnostics is { } diag)
        {
            Console.WriteLine($"    Tokens: {diag.AggregateTokenUsage.TotalTokens} | Duration: {diag.TotalDuration.TotalMilliseconds:F0}ms | Tools: {diag.ToolCalls.Count}");
        }
    }

    if (pipelineResult.AggregateTokenUsage is { } aggTokens)
    {
        Console.WriteLine($"  --- Pipeline totals: {aggTokens.TotalTokens} tokens, {pipelineResult.TotalDuration.TotalMilliseconds:F0}ms ---");
    }

    Console.WriteLine();
}

Console.WriteLine("=== Demo 4: Layer 2 Termination — Stop Pipeline After Editor ===");
Console.WriteLine("  EditorSeqAgent declares [WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), ...)].");
Console.WriteLine("  RunContentPipelineSequentialWorkflowAsync() is generated — conditions are baked in.");
Console.WriteLine("  When the editor's turn ends and the keyword is found, the publisher is skipped.");
Console.WriteLine();

var earlyTopic = "The future of agentic AI pipelines";
Console.WriteLine($"Topic: {earlyTopic}");

var earlyResponses = await workflowFactory.RunContentPipelineSequentialWorkflowAsync(earlyTopic);

foreach (var (executorId, text) in earlyResponses)
{
    Console.WriteLine($"  [{executorId}]: {text.Trim()}");
}

var publisherRan = earlyResponses.Keys.Any(k =>
    k == AgentNames.PublisherSeqAgent || k.StartsWith($"{AgentNames.PublisherSeqAgent}_", StringComparison.Ordinal));
Console.WriteLine();
Console.WriteLine(publisherRan
    ? "  (publisher ran — STATUS: EDIT_COMPLETE keyword was not found in editor response)"
    : "  Terminated early: PublisherSeqAgent was skipped.");
Console.WriteLine();
