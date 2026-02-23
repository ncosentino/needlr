using Azure;
using Azure.AI.OpenAI;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
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

// Needlr discovers all [AgentFunction] methods across the assembly at startup.
// IAgentFactory is injected wherever you need to create agents.
var agentFactory = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .AddAgentFunctionsFromAssemblies())
    .BuildServiceProvider(configuration)
    .GetRequiredService<IAgentFactory>();

// ResearchAgent: Needlr auto-wires ALL discovered [AgentFunction] tools.
// No manual AIFunctionFactory.Create() calls needed.
var researchAgent = agentFactory.CreateAgent(opts =>
    opts.Instructions = """
        You are a research assistant. Use your tools to gather all available facts about Nick —
        his favorite cities, the countries he has lived in, his hobbies, and his food preferences.
        Compile a thorough summary of everything you find. Include all details.
        """);

// WriterAgent: pure LLM — no tools needed. FunctionTypes = [] opts it out of all functions.
var writerAgent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = """
        You are a skilled writer. You will receive a research summary about a person named Nick.
        Turn it into a short, engaging personal profile — friendly, narrative tone, two paragraphs.
        """;
    opts.FunctionTypes = [];
});

// Connect the agents: research output flows directly into the writer via the MAF workflow graph.
var workflow = new WorkflowBuilder(researchAgent)
    .AddEdge(researchAgent, writerAgent)
    .Build();

Console.WriteLine("=== Needlr + Microsoft Agent Framework: Multi-Agent Research Pipeline ===");
Console.WriteLine();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow,
    new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "Research everything about Nick and pass it along."));

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

string currentExecutor = "";
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent update && !string.IsNullOrEmpty(update.Data?.ToString()))
    {
        if (currentExecutor != update.ExecutorId)
        {
            if (currentExecutor != "")
                Console.WriteLine();
            currentExecutor = update.ExecutorId;
            Console.WriteLine($"--- {currentExecutor} ---");
        }

        Console.Write(update.Data);
    }
    else if (evt is ExecutorFailedEvent failure)
    {
        Console.Error.WriteLine($"[ERROR] {failure.ExecutorId}: {failure.Data?.Message ?? failure.Data?.ToString()}");
    }
    else if (evt is WorkflowErrorEvent workflowError)
    {
        Console.Error.WriteLine($"[WORKFLOW ERROR] {workflowError.Exception?.Message ?? workflowError.Exception?.ToString()}");
    }
}

Console.WriteLine();

