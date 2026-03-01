using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// Generated extension methods from SimpleAgentFrameworkApp.Agents: IAgentFactory.CreateTriageAgent(),
// AgentNames constants, etc. The [ModuleInitializer] in that assembly also registers a
// GeneratedAIFunctionProvider with AgentFrameworkGeneratedBootstrap so that UsingAgentFramework()
// takes the AOT-safe provider path instead of the [RequiresDynamicCode] reflection path.
// This project treats IL3050 as an error to verify no dynamic code generation occurs.
using SimpleAgentFrameworkApp.Agents.Generated;

var serviceProvider = new Syringe()
    .UsingSourceGen()
    .UsingAgentFramework(af => af.UsingChatClient(new NoOpChatClient()))
    .BuildServiceProvider();

var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
var triageAgent = agentFactory.CreateTriageAgent();

Console.WriteLine($"AOT-compatible agent created: {AgentNames.TriageAgent} (ID: {triageAgent.Id})");

/// <summary>
/// Minimal IChatClient for NativeAOT demonstration purposes.
/// Not AOT-blocked — proves that Needlr MAF wiring is IL3050-free.
/// </summary>
sealed class NoOpChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = new("no-op");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "No-op response")));

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Streaming not supported in no-op client.");

    public void Dispose() { }

    public object? GetService(Type serviceType, object? key = null) => null;
}