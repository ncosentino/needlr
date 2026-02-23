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

using SimpleAgentFrameworkApp;

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

// Needlr discovers all [AgentFunction] and [AgentFunctionGroup] at startup.
// IAgentFactory is injected wherever you need to create agents.
var agentFactory = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .AddAgentFunctionsFromAssemblies()
        .AddAgentFunctionGroupsFromAssemblies())
    .BuildServiceProvider(configuration)
    .GetRequiredService<IAgentFactory>();

// TriageAgent: no tools — reasons about the question and routes via handoff.
var triageAgent = agentFactory.CreateAgent(opts =>
{
    opts.Name = "TriageAgent";
    opts.Instructions = """
        You are a triage assistant for questions about Nick. Route each question to exactly one specialist:
        - GeographyAgent: cities, countries, travel, places Nick has lived
        - LifestyleAgent: hobbies, food, ice cream, daily life, interests
        Always hand off. Never answer directly.
        """;
    opts.FunctionTypes = [];
});

// GeographyAgent: answers location/travel questions using its function group.
var geographyAgent = agentFactory.CreateAgent(opts =>
{
    opts.Name = "GeographyAgent";
    opts.Instructions = """
        You are Nick's geography expert. Use your tools to look up his cities and countries,
        then give a short, friendly answer.
        """;
    opts.FunctionGroups = ["geography"];
});

// LifestyleAgent: answers hobbies/food questions using its function group.
var lifestyleAgent = agentFactory.CreateAgent(opts =>
{
    opts.Name = "LifestyleAgent";
    opts.Instructions = """
        You are Nick's lifestyle expert. Use your tools to look up his hobbies and food preferences,
        then give a short, friendly answer.
        """;
    opts.FunctionGroups = ["lifestyle"];
});

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

