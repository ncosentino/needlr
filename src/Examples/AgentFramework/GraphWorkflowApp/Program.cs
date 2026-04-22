// =============================================================================
// Graph / DAG Workflow Example
// =============================================================================
// Demonstrates Needlr's DAG workflow support: attribute-declared graph topology,
// source-generated factory methods, and fan-out/fan-in execution via MAF.
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
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.Copilot;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

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
    .UsingAgentFramework(af => af.UsingChatClient(chatClient))
    .BuildServiceProvider(configuration);

var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();

Console.WriteLine("=== Needlr + MAF: DAG Graph Workflow — Research Pipeline ===");
Console.WriteLine($"  LLM:       Copilot ({copilotOptions.DefaultModel})");
Console.WriteLine("  Topology:  AnalyzerAgent → [WebResearchAgent, DatabaseAgent] → SummarizerAgent");
Console.WriteLine("  Executor:  RunGraphAsync (auto-selects MAF BSP for WaitAll, Needlr-native for WaitAny)");
Console.WriteLine();

const string question = "What are the key trends in AI agent frameworks for 2025?";
Console.WriteLine($"Question: {question}");
Console.WriteLine();
Console.WriteLine("--- Agent responses ---");
Console.WriteLine();

// RunGraphAsync auto-selects the executor:
// - If all nodes use WaitAll (default) → MAF's native BSP executor via InProcessExecution
// - If any node uses WaitAny → Needlr's own executor using Task.WhenAny at fan-in points
var responses = await workflowFactory.RunGraphAsync("research-pipeline", question);

foreach (var (agentName, text) in responses)
{
    Console.WriteLine($"[{agentName}]");
    Console.WriteLine(text.Trim());
    Console.WriteLine();
}

Console.WriteLine("=== Done ===");
