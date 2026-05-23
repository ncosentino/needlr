// =============================================================================
// GenAI Token Metrics Example
// =============================================================================
// Demonstrates Needlr's IGenAiTokenMetrics co-emitting cache_read and reasoning
// measurements on the OpenTelemetry gen_ai.client.token.usage histogram —
// alongside MEAI's OpenTelemetryChatClient which emits input and output on the
// same histogram. End result: a single histogram series carrying all four
// gen_ai.token.type values per LLM call.
//
// Uses a mock chat client that returns UsageDetails with all four token counts
// populated, so no LLM credentials are required.
// =============================================================================

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

Console.WriteLine("=== GenAI Token Metrics Example ===");
Console.WriteLine();

const string SharedMeterName = "Example.GenAI";

Console.WriteLine($"[SETUP] Shared meter: '{SharedMeterName}'");
Console.WriteLine("[SETUP] MEAI's OpenTelemetryChatClient and Needlr's GenAiTokenMetrics");
Console.WriteLine("[SETUP] both write to the same meter so the resulting histogram series");
Console.WriteLine("[SETUP] carries all four gen_ai.token.type values per LLM call.");
Console.WriteLine();

var capturedSamples = new ConcurrentBag<HistogramSample>();
using var meterListener = new MeterListener();
meterListener.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Meter.Name == SharedMeterName
        && instrument.Name == "gen_ai.client.token.usage")
    {
        listener.EnableMeasurementEvents(instrument);
    }
};
meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
{
    var dict = new Dictionary<string, object?>(tags.Length);
    foreach (var tag in tags)
    {
        dict[tag.Key] = tag.Value;
    }
    capturedSamples.Add(new HistogramSample(measurement, dict));
});
meterListener.Start();

Console.WriteLine("[SETUP] MeterListener attached to 'gen_ai.client.token.usage'.");
Console.WriteLine();

var mockProvider = new MockChatClientWithFullUsage();
var meaiWrappedClient = new OpenTelemetryChatClient(mockProvider, sourceName: SharedMeterName);

var config = new ConfigurationBuilder().Build();

var sp = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .ConfigureMetrics(o => o.GenAiMeterName = SharedMeterName)
        .Configure(opts => opts.ChatClientFactory = _ => meaiWrappedClient)
        .UsingDiagnostics())
    .BuildServiceProvider(config);

Console.WriteLine("[SETUP] Syringe wired:");
Console.WriteLine($"[SETUP]   GenAiMeterName = '{SharedMeterName}'");
Console.WriteLine("[SETUP]   ChatClientFactory: OpenTelemetryChatClient(MockChatClient)");
Console.WriteLine("[SETUP]   UsingDiagnostics(): installs Needlr's DiagnosticsChatClientMiddleware");
Console.WriteLine();

var loop = sp.GetRequiredService<IIterativeAgentLoop>();

Console.WriteLine("[RUN] Calling IIterativeAgentLoop.RunAsync (1 LLM call expected)...");
Console.WriteLine();

var loopOptions = new IterativeLoopOptions
{
    Instructions = "You are an agent that summarises cached prompt content.",
    PromptFactory = _ => "Summarize the cached prompt content.",
    Tools = [],
    MaxIterations = 1,
    IsComplete = _ => true,
    LoopName = "gen-ai-token-metrics-demo",
};

var result = await loop.RunAsync(
    loopOptions,
    new IterativeContext { Workspace = new InMemoryWorkspace() },
    CancellationToken.None);

meterListener.Dispose();

Console.WriteLine($"[RUN] Loop completed: {result.Iterations.Count} iteration(s)");
Console.WriteLine();

Console.WriteLine("=== Captured `gen_ai.client.token.usage` samples ===");
Console.WriteLine();

var byType = capturedSamples
    .GroupBy(s => (string?)s.Tags.GetValueOrDefault("gen_ai.token.type") ?? "(missing)")
    .OrderBy(g => g.Key, StringComparer.Ordinal)
    .ToList();

foreach (var group in byType)
{
    var origin = group.Key is "input" or "output" ? "MEAI" : "Needlr";
    var sample = group.First();
    Console.WriteLine($"  gen_ai.token.type=\"{group.Key,-10}\" sample={sample.Value,6}  (emitted by {origin})");
    foreach (var kvp in sample.Tags.OrderBy(k => k.Key, StringComparer.Ordinal))
    {
        if (kvp.Key == "gen_ai.token.type") continue;
        Console.WriteLine($"    {kvp.Key,-26} = {kvp.Value ?? "(null)"}");
    }
    Console.WriteLine();
}

Console.WriteLine("=== Verification ===");
Console.WriteLine();

var expectedTypes = new[] { "input", "output", "cache_read", "reasoning" };
var observedTypes = byType.Select(g => g.Key).ToHashSet();
var passed = true;

foreach (var expected in expectedTypes)
{
    if (observedTypes.Contains(expected))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [ok] gen_ai.token.type=\"{expected}\" present");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [FAIL] gen_ai.token.type=\"{expected}\" missing");
        Console.ResetColor();
        passed = false;
    }
}

var inputCount = byType.SingleOrDefault(g => g.Key == "input")?.Count() ?? 0;
var outputCount = byType.SingleOrDefault(g => g.Key == "output")?.Count() ?? 0;

if (inputCount == 1 && outputCount == 1)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  [ok] input and output each appear exactly once (no Needlr/MEAI double-counting)");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [FAIL] expected exactly one input and one output sample, got input={inputCount}, output={outputCount}");
    Console.ResetColor();
    passed = false;
}

Console.WriteLine();
if (passed)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("All checks passed. Needlr + MEAI cohabit on the gen_ai.client.token.usage histogram.");
    Console.ResetColor();
    return 0;
}

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("CHECKS FAILED — see above.");
Console.ResetColor();
return 1;

internal sealed record HistogramSample(int Value, Dictionary<string, object?> Tags);

internal sealed class MockChatClientWithFullUsage : IChatClient
{
    private readonly ChatClientMetadata _metadata = new(
        providerName: "mock-provider",
        providerUri: new Uri("https://api.example.com:443"),
        defaultModelId: "mock-model");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Mock summary response.")])
        {
            ModelId = "mock-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 5000,
                OutputTokenCount = 250,
                TotalTokenCount = 5250,
                CachedInputTokenCount = 3000,
                ReasoningTokenCount = 150,
            },
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming not used in this example.");

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(ChatClientMetadata))
            return _metadata;
        return null;
    }
}
