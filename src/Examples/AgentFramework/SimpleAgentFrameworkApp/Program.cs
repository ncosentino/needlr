using Azure;
using Azure.AI.OpenAI;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

// Generated extension methods: CreateTriageHandoffWorkflow() and CreateContentPipelineSequentialWorkflow()
// on IWorkflowFactory. These are emitted by the source generator in SimpleAgentFrameworkApp.Agents based on
// [AgentHandoffsTo] on TriageAgent and [AgentSequenceMember] on Writer/Editor/PublisherSeqAgent.
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
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAssemblyProvider(b => b.MatchingAssemblies(path =>
        path.Contains("SimpleAgentFrameworkApp", StringComparison.OrdinalIgnoreCase)).Build())
    .UsingAgentFramework(af => af.UsingChatClient(chatClient))
    .BuildServiceProvider(configuration);

var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();

// --- Demo 1: Handoff workflow ---
// CreateTriageHandoffWorkflow() is generated because TriageAgent is decorated with [AgentHandoffsTo].
var handoffWorkflow = workflowFactory.CreateTriageHandoffWorkflow();

Console.WriteLine("=== Demo 1: Triage → Handoff Multi-Agent Workflow ===");
Console.WriteLine("  Topology declared via [AgentHandoffsTo] on TriageAgent.");
Console.WriteLine();

var questions = new[]
{
    "Which countries has Nick lived in?",
    "What are Nick's top hobbies?",
};

foreach (var question in questions)
{
    Console.WriteLine($"Q: {question}");

    await using var run = await InProcessExecution.RunStreamingAsync(
        handoffWorkflow,
        new ChatMessage(ChatRole.User, question));

    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    var responses = await run.CollectAgentResponsesAsync();

    foreach (var (executorId, text) in responses)
    {
        Console.WriteLine($"  [{executorId}]: {text.Trim()}");
    }

    Console.WriteLine();
}

// --- Demo 2: Sequential pipeline workflow ---
// CreateContentPipelineSequentialWorkflow() is generated because WriterSeqAgent, EditorSeqAgent,
// and PublisherSeqAgent are decorated with [AgentSequenceMember("content-pipeline", 1/2/3)].
// The topology is fully declared on agent classes — Program.cs never references them directly.
var sequentialWorkflow = workflowFactory.CreateContentPipelineSequentialWorkflow();

Console.WriteLine("=== Demo 2: Content Pipeline Sequential Workflow ===");
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

    await using var run = await InProcessExecution.RunStreamingAsync(
        sequentialWorkflow,
        new ChatMessage(ChatRole.User, topic));

    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    var responses = await run.CollectAgentResponsesAsync();

    foreach (var (executorId, text) in responses)
    {
        Console.WriteLine($"  [{executorId}]: {text.Trim()}");
    }

    Console.WriteLine();
}

