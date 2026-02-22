using Azure;
using Azure.AI.OpenAI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;

using SimpleAgentFrameworkApp;
using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

// All function types are registered once. Each agent created from the factory
// can restrict which functions it has access to via AgentFactoryOptions.FunctionTypes —
// a capability the SK integration does not support at the factory level.
var agentFactory = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .Configure(opts =>
        {
            // MAF is built on Microsoft.Extensions.AI — configure IChatClient,
            // not a Kernel builder like in the SK integration.
            var azureSection = opts.ServiceProvider
                .GetRequiredService<IConfiguration>()
                .GetSection("AzureOpenAI");

            opts.ChatClientFactory = sp => new AzureOpenAIClient(
                    new Uri(azureSection["Endpoint"]
                        ?? throw new InvalidOperationException("No AzureOpenAI:Endpoint set")),
                    new AzureKeyCredential(azureSection["ApiKey"]
                        ?? throw new InvalidOperationException("No AzureOpenAI:ApiKey set")))
                .GetChatClient(azureSection["DeploymentName"]
                    ?? throw new InvalidOperationException("No AzureOpenAI:DeploymentName set"))
                .AsIChatClient();
        })
        .AddAgentFunctionsFromAssemblies())
    .BuildServiceProvider(configuration)
    .GetRequiredService<IAgentFactory>();

// Geography agent — only has access to geography functions.
var geographyAgent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = "You are Nick's travel advisor. Answer questions about his locations only. " +
                        "Use the available functions to look up information about his cities and countries.";
    opts.FunctionTypes = [typeof(GeographyFunctions)];
});

// Lifestyle agent — only has access to lifestyle functions.
var lifestyleAgent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = "You are Nick's lifestyle coach. Answer questions about his hobbies and food preferences. " +
                        "Use the available functions to look up information about his interests.";
    opts.FunctionTypes = [typeof(LifestyleFunctions)];
});

// Each agent gets its own session — multi-turn conversation state is tracked separately.
var geoSession = await geographyAgent.CreateSessionAsync();
var lifestyleSession = await lifestyleAgent.CreateSessionAsync();

Console.WriteLine("=== Geography Agent ===");
await AskAsync(geographyAgent, geoSession, "What are Nick's favorite cities?");
await AskAsync(geographyAgent, geoSession, "Which of those are in Europe?");
await AskAsync(geographyAgent, geoSession, "Which country has he lived in longest?");

Console.WriteLine();
Console.WriteLine("=== Lifestyle Agent ===");
await AskAsync(lifestyleAgent, lifestyleSession, "What hobbies does Nick enjoy?");
await AskAsync(lifestyleAgent, lifestyleSession, "Which of those are outdoors activities?");
await AskAsync(lifestyleAgent, lifestyleSession, "What is his favorite ice cream?");

static async Task AskAsync(
    Microsoft.Agents.AI.AIAgent agent,
    Microsoft.Agents.AI.AgentSession session,
    string question)
{
    Console.WriteLine($"QUESTION: {question}");
    var result = await agent.RunAsync(question, session);
    Console.WriteLine($"ANSWER:   {result.Text}");
    Console.WriteLine();
}
