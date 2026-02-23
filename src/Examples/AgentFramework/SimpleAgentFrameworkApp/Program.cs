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

// TriageAgent: no tools — it reasons about the question and routes via handoff.
var triageAgent = agentFactory.CreateAgent(opts =>
{
    opts.Name = "TriageAgent";
    opts.Instructions = """
        You are a triage assistant. Based on the question, decide whether to hand off to the
        GeographyAgent (for questions about Nick's cities, countries, travel) or the LifestyleAgent
        (for questions about Nick's hobbies, food preferences, or daily life).
        Do not answer directly — always hand off to the right specialist.
        """;
    opts.FunctionTypes = [];
});

// GeographyAgent: has access only to geography functions via the "geography" group.
var geographyAgent = agentFactory.CreateAgent(opts =>
{
    opts.Name = "GeographyAgent";
    opts.Instructions = "You are Nick's geography expert. Answer questions about his cities and countries.";
    opts.FunctionGroups = ["geography"];
});

// LifestyleAgent: has access only to lifestyle functions via the "lifestyle" group.
var lifestyleAgent = agentFactory.CreateAgent(opts =>
{
    opts.Name = "LifestyleAgent";
    opts.Instructions = "You are Nick's lifestyle expert. Answer questions about his hobbies and food.";
    opts.FunctionGroups = ["lifestyle"];
});

// BuildHandoffWorkflow is a Needlr helper that wraps MAF's handoff builder.
// It hides the asymmetric API (where you'd otherwise pass triageAgent twice).
var workflow = agentFactory.BuildHandoffWorkflow(
    triageAgent,
    (geographyAgent, "For questions about Nick's cities, countries, or travel"),
    (lifestyleAgent, "For questions about Nick's hobbies, food, or daily life"));

Console.WriteLine("=== Needlr + MAF: Triage → Handoff Multi-Agent Workflow ===");
Console.WriteLine();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow,
    new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "What are Nick's favorite cities and his top hobbies?"));

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

// CollectAgentResponsesAsync is a Needlr helper that replaces the manual
// await-foreach + Dictionary<string, StringBuilder> pattern.
var responses = await run.CollectAgentResponsesAsync();

foreach (var (executorId, text) in responses)
{
    Console.WriteLine($"--- {executorId} ---");
    Console.WriteLine(text);
    Console.WriteLine();
}

