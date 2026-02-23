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

using SimpleAgentFrameworkApp.Agents;

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

// Scan both the host and the plugin assembly for injectable types (e.g. PersonalDataProvider).
// The [ModuleInitializer] emitted by the source generator in SimpleAgentFrameworkApp.Agents
// fires on assembly load and registers all [NeedlrAiAgent] types, [AgentFunction] types, and
// [AgentFunctionGroup] groups with AgentFrameworkGeneratedBootstrap.
// UsingAgentFramework() detects these and auto-populates — no explicit Add* calls needed.
var agentFactory = new Syringe()
    .UsingReflection()
    .UsingAssemblyProvider(b => b.MatchingAssemblies(path =>
        path.Contains("SimpleAgentFrameworkApp", StringComparison.OrdinalIgnoreCase)).Build())
    .UsingAgentFramework(af => af.UsingChatClient(chatClient))
    .BuildServiceProvider(configuration)
    .GetRequiredService<IAgentFactory>();

// Agent creation by type — Instructions, FunctionGroups, and Description all come from
// the [NeedlrAiAgent] attribute declared in SimpleAgentFrameworkApp.Agents.
var triageAgent = agentFactory.CreateAgent<TriageAgent>();
var geographyAgent = agentFactory.CreateAgent<GeographyAgent>();
var lifestyleAgent = agentFactory.CreateAgent<LifestyleAgent>();

// BuildHandoffWorkflow hides MAF's asymmetric builder API — no need to pass triageAgent twice.
// With reasons, the LLM has explicit guidance on when to route to each specialist.
var workflow = agentFactory.BuildHandoffWorkflow(
    triageAgent,
    (geographyAgent, "For questions about Nick's cities, countries, or places he has lived"),
    (lifestyleAgent, "For questions about Nick's hobbies, food preferences, or daily life"));

Console.WriteLine("=== Needlr + MAF: Triage → Handoff Multi-Agent Workflow ===");
Console.WriteLine();

var questions = new[]
{
    "Which countries has Nick lived in?",
    "What are Nick's top hobbies?",
    "What's Nick's favorite ice cream?",
    "What cities does Nick love?",
};

foreach (var question in questions)
{
    Console.WriteLine($"Q: {question}");

    await using var run = await InProcessExecution.RunStreamingAsync(
        workflow,
        new ChatMessage(ChatRole.User, question));

    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    // CollectAgentResponsesAsync replaces the manual await-foreach + Dictionary<string,StringBuilder>.
    var responses = await run.CollectAgentResponsesAsync();

    foreach (var (executorId, text) in responses)
    {
        Console.WriteLine($"  [{executorId}]: {text.Trim()}");
    }

    Console.WriteLine();
}

