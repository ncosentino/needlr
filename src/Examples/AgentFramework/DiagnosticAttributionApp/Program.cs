// =============================================================================
// Diagnostic Attribution Example
// =============================================================================
// Demonstrates that tool call and chat completion diagnostics carry the correct
// AgentName in multi-agent scenarios, enabling unambiguous attribution after
// flattening/aggregation.
//
// This example uses in-process mock chat clients — no LLM credentials required.
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

Console.WriteLine("=== Diagnostic Attribution Example ===");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 1. Build a service provider with diagnostics enabled and a mock chat client
//    that returns a simple response with tool-call-like function content.
// ---------------------------------------------------------------------------

var config = new ConfigurationBuilder().Build();

var mockChat = new MockChatClient();

var sp = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .Configure(opts => opts.ChatClientFactory = _ => mockChat)
        .UsingDiagnostics())
    .BuildServiceProvider(config);

var factory = sp.GetRequiredService<IAgentFactory>();
var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

// ---------------------------------------------------------------------------
// 2. Run two agents sequentially and collect diagnostics from each
// ---------------------------------------------------------------------------

Console.WriteLine("[STEP 1] Running ColdReader agent...");
var coldReader = factory.CreateAgent(opts =>
{
    opts.Name = "ColdReader";
    opts.Instructions = "You are a cold reader agent.";
});

IAgentRunDiagnostics coldReaderDiag;
using (diagAccessor.BeginCapture())
{
    await coldReader.RunAsync("Analyze the article", cancellationToken: CancellationToken.None);
    coldReaderDiag = diagAccessor.LastRunDiagnostics!;
}

Console.WriteLine($"  ColdReader diagnostics: {coldReaderDiag.ChatCompletions.Count} completions, " +
    $"{coldReaderDiag.ToolCalls.Count} tool calls");

Console.WriteLine();
Console.WriteLine("[STEP 2] Running RevisionWriter agent...");
var revisionWriter = factory.CreateAgent(opts =>
{
    opts.Name = "RevisionWriter";
    opts.Instructions = "You are a revision writer agent.";
});

IAgentRunDiagnostics revisionWriterDiag;
using (diagAccessor.BeginCapture())
{
    await revisionWriter.RunAsync("Write the revision", cancellationToken: CancellationToken.None);
    revisionWriterDiag = diagAccessor.LastRunDiagnostics!;
}

Console.WriteLine($"  RevisionWriter diagnostics: {revisionWriterDiag.ChatCompletions.Count} completions, " +
    $"{revisionWriterDiag.ToolCalls.Count} tool calls");

// ---------------------------------------------------------------------------
// 3. Flatten diagnostics — this is what consumers do when aggregating across
//    multiple agent runs. BEFORE this fix, AgentName would be null on tool calls.
// ---------------------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== AGGREGATED VIEW (flattened across agents) ===");
Console.WriteLine();

var allToolCalls = coldReaderDiag.ToolCalls
    .Concat(revisionWriterDiag.ToolCalls)
    .ToList();

var allCompletions = coldReaderDiag.ChatCompletions
    .Concat(revisionWriterDiag.ChatCompletions)
    .ToList();

Console.WriteLine($"Total tool calls: {allToolCalls.Count}");
Console.WriteLine($"Total completions: {allCompletions.Count}");
Console.WriteLine();

// Print each tool call with its agent attribution
Console.WriteLine("--- Tool Calls ---");
if (allToolCalls.Count == 0)
{
    Console.WriteLine("  (none — the mock client doesn't trigger tool calls,");
    Console.WriteLine("   but the AgentName property is wired for when they do)");
}
else
{
    foreach (var tc in allToolCalls)
    {
        Console.WriteLine($"  [{tc.AgentName ?? "NULL (BROKEN)"}] {tc.ToolName} " +
            $"({tc.Duration.TotalMilliseconds:F0}ms, {(tc.Succeeded ? "ok" : "FAILED")})");
    }
}

Console.WriteLine();

// Print each chat completion with its agent attribution
Console.WriteLine("--- Chat Completions ---");
foreach (var cc in allCompletions)
{
    Console.WriteLine($"  [{cc.AgentName ?? "NULL (BROKEN)"}] model={cc.Model}, " +
        $"in={cc.Tokens.InputTokens}, out={cc.Tokens.OutputTokens}, " +
        $"{cc.Duration.TotalMilliseconds:F0}ms");
}

// ---------------------------------------------------------------------------
// 4. Verify attribution correctness
// ---------------------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== ATTRIBUTION VERIFICATION ===");

var coldReaderCompletions = allCompletions.Where(c => c.AgentName == "ColdReader").Count();
var revisionWriterCompletions = allCompletions.Where(c => c.AgentName == "RevisionWriter").Count();
var nullCompletions = allCompletions.Where(c => c.AgentName is null).Count();

Console.WriteLine($"  ColdReader completions:      {coldReaderCompletions}");
Console.WriteLine($"  RevisionWriter completions:  {revisionWriterCompletions}");
Console.WriteLine($"  Null AgentName completions:  {nullCompletions}");

if (nullCompletions > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ✗ FAILED — some completions have null AgentName!");
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("  ✓ PASSED — all completions have correct AgentName attribution!");
Console.ResetColor();

// ---------------------------------------------------------------------------
// 5. Demonstrate nested builder stack safety
// ---------------------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== NESTED BUILDER STACK SAFETY ===");

using var outerBuilder = AgentRunDiagnosticsBuilder.StartNew("OuterAgent");
Console.WriteLine($"  Current builder: {AgentRunDiagnosticsBuilder.GetCurrent()!.AgentName}");

using (var innerBuilder = AgentRunDiagnosticsBuilder.StartNew("InnerAgent"))
{
    Console.WriteLine($"  After inner StartNew: {AgentRunDiagnosticsBuilder.GetCurrent()!.AgentName}");
    Console.WriteLine($"  Inner ParentAgentName: {innerBuilder.ParentAgentName}");
}

Console.WriteLine($"  After inner Dispose: {AgentRunDiagnosticsBuilder.GetCurrent()!.AgentName}");

if (AgentRunDiagnosticsBuilder.GetCurrent()!.AgentName == "OuterAgent")
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  ✓ PASSED — outer builder correctly restored after inner dispose!");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("  ✗ FAILED — outer builder NOT restored!");
    Console.ResetColor();
    return 1;
}

Console.WriteLine();
Console.WriteLine("All checks passed.");
return 0;

// =============================================================================
// Mock chat client — no LLM required
// =============================================================================

internal sealed class MockChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Mock response")])
        {
            ModelId = "mock-model"
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
