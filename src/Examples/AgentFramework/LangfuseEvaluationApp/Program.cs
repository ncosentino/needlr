// =============================================================================
// LangfuseEvaluationApp
// -----------------------------------------------------------------------------
// Demonstrates how little code it takes to send Needlr agent telemetry AND
// Microsoft.Extensions.AI.Evaluation scores to Langfuse.
//
// The Langfuse integration is four calls:
//   1. LangfuseTelemetry.Start(...)        -> exports gen_ai traces/metrics
//   2. session.BeginScenario(...)          -> one Langfuse trace per eval scenario
//   3. scenario.RecordEvaluationAsync(...) -> evaluator metrics become Langfuse scores
//   4. session.Shutdown(...)                -> bounded final drain + resource release
//
// Everything else here is an ordinary Needlr agent run + evaluation. A mock
// chat client is used so the example runs with no LLM credentials, and the
// Langfuse calls no-op cleanly when LANGFUSE_PUBLIC_KEY / LANGFUSE_SECRET_KEY
// are not set — so this example always runs end to end.
// =============================================================================

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.AgentFramework.Langfuse;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

Console.WriteLine("=== Needlr → Langfuse Evaluation Example ===");
Console.WriteLine();

// ── Wire a Needlr agent with diagnostics and a credential-free mock client ──
var configuration = new ConfigurationBuilder().Build();

var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .Configure(opts => opts.ChatClientFactory = _ => new MockChatClient())
        .UsingDiagnostics())
    .BuildServiceProvider(configuration);

var loop = serviceProvider.GetRequiredService<IIterativeAgentLoop>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

// ── Start Langfuse export (call #1) ─────────────────────────────────────────
var langfuseOptions = LangfuseOptions.FromEnvironment();
langfuseOptions.ServiceName = "needlr-langfuse-demo";
langfuseOptions.ScoreErrorCallback = error =>
    Console.WriteLine($"[langfuse] score '{error.ScoreName}' was not recorded: {error.Exception.Message}");
langfuseOptions.DiagnosticsCallback = message => Console.WriteLine($"[langfuse] {message}");

using var langfuse = LangfuseTelemetry.Start(langfuseOptions);

if (langfuse.IsEnabled)
{
    Console.WriteLine("[langfuse] Export ENABLED — traces and scores will be sent to Langfuse.");
}
else
{
    Console.WriteLine("[langfuse] Export DISABLED (no credentials or no target).");
    Console.WriteLine("[langfuse] Set LANGFUSE_PUBLIC_KEY and LANGFUSE_SECRET_KEY, plus a target —");
    Console.WriteLine("[langfuse] LANGFUSE_HOST (self-hosted) or a Region (cloud) — to send this run to Langfuse.");
}

Console.WriteLine();

var runId = $"demo-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
IAgentRunDiagnostics? diagnostics;

// ── One Langfuse trace per scenario (call #2) ───────────────────────────────
using (var scenario = langfuse.BeginScenario(
    name: "eval: cached-summary",
    sessionId: runId,
    tags: ["demo", "efficiency", "coherence"],
    metadata: new Dictionary<string, string> { ["suite"] = "needlr-langfuse-demo" }))
{
    Console.WriteLine($"[run] Scenario trace id: {scenario.TraceId ?? "(export disabled)"}");

    var loopOptions = new IterativeLoopOptions
    {
        Instructions = "You summarise cached prompt content.",
        PromptFactory = _ => "Summarize the cached prompt content.",
        Tools = [],
        MaxIterations = 1,
        IsComplete = _ => true,
        LoopName = "langfuse-eval-demo",
    };

    // Agent telemetry produced here nests under the scenario trace above.
    using (diagnosticsAccessor.BeginCapture())
    {
        await loop.RunAsync(
            loopOptions,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            CancellationToken.None);
        diagnostics = diagnosticsAccessor.LastRunDiagnostics;
    }

    if (diagnostics is null)
    {
        Console.WriteLine("[run] No diagnostics captured — cannot evaluate.");
        return 1;
    }

    // ── Score the run with deterministic Needlr evaluators (no LLM needed) ──
    var evalInputs = diagnostics.ToEvaluationInputs();
    var diagnosticsContext = new AgentRunDiagnosticsContext(diagnostics);

    var efficiency = await new EfficiencyEvaluator(tokenBudget: 200_000).EvaluateAsync(
        evalInputs.Messages,
        evalInputs.ModelResponse,
        additionalContext: [diagnosticsContext]);

    var coherence = await new IterationCoherenceEvaluator(maxIterations: 20).EvaluateAsync(
        evalInputs.Messages,
        evalInputs.ModelResponse,
        additionalContext: [diagnosticsContext]);

    PrintMetrics("EfficiencyEvaluator", efficiency);
    PrintMetrics("IterationCoherenceEvaluator", coherence);

    // ── Project evaluator metrics to Langfuse scores (call #3) ──────────────
    // Score uploads are non-fatal by default: a Langfuse outage records a failure
    // (ScoreErrorCallback + ScoresFailed) but never fails the eval itself.
    await efficiency.RecordLangfuseScoresAsync(scenario);
    await coherence.RecordLangfuseScoresAsync(scenario);

    Console.WriteLine();
    if (!langfuse.IsEnabled)
    {
        Console.WriteLine("[langfuse] (Scores skipped — export disabled.)");
    }
    else if (langfuse.ScoresFailed == 0)
    {
        Console.WriteLine("[langfuse] Recorded evaluator metrics as scores on the scenario trace.");
    }
    else
    {
        Console.WriteLine($"[langfuse] {langfuse.ScoresFailed} score(s) failed to upload (eval still passed).");
    }
}

// Scenario disposed (root span ended); perform one bounded final drain.
var shutdown = langfuse.Shutdown(langfuseOptions.ShutdownTimeout);
Console.WriteLine($"[langfuse] Shutdown: traces={shutdown.Traces}, metrics={shutdown.Metrics}");

Console.WriteLine();
Console.WriteLine("Done. With credentials set, open Langfuse and find the trace named");
Console.WriteLine("'eval: cached-summary' to see the run, its gen_ai spans, and the scores.");
return 0;

static void PrintMetrics(string evaluatorName, Microsoft.Extensions.AI.Evaluation.EvaluationResult result)
{
    Console.WriteLine();
    Console.WriteLine($"[eval] {evaluatorName} — {result.Metrics.Count} metric(s):");
    foreach (var metric in result.Metrics.Values)
    {
        var value = metric switch
        {
            Microsoft.Extensions.AI.Evaluation.NumericMetric nm => nm.Value?.ToString("F2") ?? "n/a",
            Microsoft.Extensions.AI.Evaluation.BooleanMetric bm => bm.Value?.ToString() ?? "n/a",
            Microsoft.Extensions.AI.Evaluation.StringMetric sm => sm.Value ?? "n/a",
            _ => "n/a",
        };
        Console.WriteLine($"        • {metric.Name} = {value}");
    }
}

internal sealed class MockChatClient : IChatClient
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
            [new ChatMessage(ChatRole.Assistant, "Mock summary of the cached prompt content.")])
        {
            ModelId = "mock-model",
            Usage = new UsageDetails
            {
                InputTokenCount = 4000,
                OutputTokenCount = 180,
                TotalTokenCount = 4180,
                CachedInputTokenCount = 2500,
                ReasoningTokenCount = 90,
            },
        };
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is not used in this example.");

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(ChatClientMetadata) ? _metadata : null;
    }
}
