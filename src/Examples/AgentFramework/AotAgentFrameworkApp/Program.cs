using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// Generated extension methods from SimpleAgentFrameworkApp.Agents: IAgentFactory.CreateTriageAgent(),
// IWorkflowFactory.CreateTriageHandoffWorkflow(), IWorkflowFactory.CreateContentPipelineSequentialWorkflow(),
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
var workflowFactory = serviceProvider.GetRequiredService<IWorkflowFactory>();

// --- Agent creation ---
var triageAgent = agentFactory.CreateTriageAgent();
Console.WriteLine($"AOT agent created: {AgentNames.TriageAgent} (ID: {triageAgent.Id})");
Console.WriteLine();

// --- Demo 1: Handoff workflow ---
// CreateTriageHandoffWorkflow() is generated from [AgentHandoffsTo] on TriageAgent.
// Topology wiring is verified at compile time — no reflection, no magic strings.
Console.WriteLine("=== Demo 1: Handoff Workflow (TriageAgent → Geography/Lifestyle) ===");
var handoffWorkflow = workflowFactory.CreateTriageHandoffWorkflow();
var handoffResponses = await handoffWorkflow.RunAsync("Which countries has Nick lived in?");
foreach (var (executorId, text) in handoffResponses)
    Console.WriteLine($"  [{executorId}]: {text.Trim()}");
Console.WriteLine();

// --- Demo 2: Sequential pipeline ---
// CreateContentPipelineSequentialWorkflow() is generated from [AgentSequenceMember] on Writer/Editor/Publisher.
Console.WriteLine("=== Demo 2: Sequential Pipeline (Writer → Editor → Publisher) ===");
var sequentialWorkflow = workflowFactory.CreateContentPipelineSequentialWorkflow();
var sequentialResponses = await sequentialWorkflow.RunAsync("The benefits of multi-agent AI workflows");
foreach (var (executorId, text) in sequentialResponses)
    Console.WriteLine($"  [{executorId}]: {text.Trim()}");
Console.WriteLine();

// --- Demo 3: Pipeline with termination condition ---
// RunContentPipelineSequentialWorkflowAsync() is generated — termination conditions are baked in.
// EditorSeqAgent declares [WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "STATUS: EDIT_COMPLETE")].
Console.WriteLine("=== Demo 3: Pipeline with Termination Condition ===");
var earlyResponses = await workflowFactory.RunContentPipelineSequentialWorkflowAsync(
    "Why C# source generators improve developer experience");
foreach (var (executorId, text) in earlyResponses)
    Console.WriteLine($"  [{executorId}]: {text.Trim()}");

var publisherRan = earlyResponses.Keys.Any(k =>
    k == AgentNames.PublisherSeqAgent || k.StartsWith($"{AgentNames.PublisherSeqAgent}_", StringComparison.Ordinal));
Console.WriteLine(publisherRan
    ? "  (publisher ran — STATUS: EDIT_COMPLETE keyword not found)"
    : "  Terminated early — PublisherSeqAgent skipped.");

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
