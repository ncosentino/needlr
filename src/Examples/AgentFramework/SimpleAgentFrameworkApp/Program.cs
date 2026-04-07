using Azure;
using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.AgentFramework.Workflows.Budget;
using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

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
        .UsingTokenBudget())
    .BuildServiceProvider(configuration);

var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();
var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
var tokenBudgetTracker = serviceProvider.GetRequiredService<ITokenBudgetTracker>();
var contextAccessor = serviceProvider.GetRequiredService<IAgentExecutionContextAccessor>();

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

var executionContext = new AgentExecutionContext(
    UserId: "demo-user-42",
    OrchestrationId: $"demo-{Guid.NewGuid():N}");

var questions = new[]
{
    "Which countries has Nick lived in?",
    "What are Nick's top hobbies?",
};

// Wrap agent execution in a context scope — tools see the UserId and OrchestrationId.
using (contextAccessor.BeginScope(executionContext))
{
    foreach (var question in questions)
    {
        Console.WriteLine($"Q: {question}");

        var responses = await handoffWorkflow.RunAsync(question);

        foreach (var (executorId, text) in responses)
        {
            Console.WriteLine($"  [{executorId}]: {text.Trim()}");
        }

        Console.WriteLine();
    }
}

// --- Demo 2: Token budget enforcement ---
Console.WriteLine("=== Demo 2: Token Budget Enforcement ===");
Console.WriteLine("  ITokenBudgetTracker.BeginScope(maxTokens) opens an AsyncLocal-scoped budget.");
Console.WriteLine("  TokenBudgetChatMiddleware checks before/after each LLM call.");
Console.WriteLine("  Exceeding the budget throws TokenBudgetExceededException.");
Console.WriteLine();

// First: run with a generous budget — should succeed.
Console.WriteLine("  [Generous budget: 100,000 tokens]");
using (tokenBudgetTracker.BeginScope(maxTokens: 100_000))
{
    var workflow = workflowFactory.CreateTriageHandoffWorkflow();
    var responses = await workflow.RunAsync("What cities does Nick like?");

    foreach (var (executorId, text) in responses)
    {
        Console.WriteLine($"    [{executorId}]: {text.Trim()}");
    }

    Console.WriteLine($"    Tokens used: {tokenBudgetTracker.CurrentTokens} / {tokenBudgetTracker.MaxTokens}");
}

Console.WriteLine();

// Second: run with a tiny budget — should throw.
Console.WriteLine("  [Tiny budget: 1 token — expect TokenBudgetExceededException]");
try
{
    using (tokenBudgetTracker.BeginScope(maxTokens: 1))
    {
        var workflow = workflowFactory.CreateTriageHandoffWorkflow();
        await workflow.RunAsync("What cities does Nick like?");
    }

    Console.WriteLine("    ERROR: Expected TokenBudgetExceededException but did not get one.");
}
catch (TokenBudgetExceededException ex)
{
    Console.WriteLine($"    Caught: {ex.Message}");
    Console.WriteLine($"    CurrentTokens={ex.CurrentTokens}, MaxTokens={ex.MaxTokens}");
}

Console.WriteLine();

// --- Demo 3: Sequential pipeline workflow ---
// CreateContentPipelineSequentialWorkflow() is generated because WriterSeqAgent, EditorSeqAgent,
// and PublisherSeqAgent are decorated with [AgentSequenceMember("content-pipeline", 1/2/3)].
// The topology is fully declared on agent classes — Program.cs never references them directly.
var sequentialWorkflow = workflowFactory.CreateContentPipelineSequentialWorkflow();

Console.WriteLine("=== Demo 3: Content Pipeline Sequential Workflow ===");
Console.WriteLine("  Pipeline declared via [AgentSequenceMember] on Writer → Editor → Publisher.");
Console.WriteLine();

var topics = new[]
{
    "Why C# source generators improve developer experience",
    "The benefits of multi-agent AI workflows",
};

foreach (var topic in topics)
{
    Console.WriteLine($"Topic: {topic}");

    var responses = await sequentialWorkflow.RunAsync(topic);

    foreach (var (executorId, text) in responses)
    {
        Console.WriteLine($"  [{executorId}]: {text.Trim()}");
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
