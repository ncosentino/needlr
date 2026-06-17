// =============================================================================
// LangfuseConformanceApp
// -----------------------------------------------------------------------------
// A SMALL, RUNNABLE Langfuse-supported eval that ALSO verifies the data actually
// landed in Langfuse by reading it back through the public API.
//
// Why this exists: unit tests cannot prove that traces/scores are ingested and
// GET-readable by a real Langfuse server (one Langfuse endpoint even returns 200
// for data it silently discards). This example closes that gap end to end.
//
// ── ASSUMPTIONS (adjust for your environment) ───────────────────────────────
//   1. A Langfuse instance is reachable at LANGFUSE_HOST. Default: http://localhost:3000
//      Stand one up locally with the official docker compose:
//        https://langfuse.com/self-hosting/docker-compose
//      (clone langfuse, `docker compose up`, create a project, copy its keys).
//   2. LANGFUSE_PUBLIC_KEY / LANGFUSE_SECRET_KEY belong to a project on that instance.
//   3. Verified against self-hosted Langfuse v3.x. Langfuse Cloud also works — set
//      LANGFUSE_HOST to your region URL (e.g. https://cloud.langfuse.com) + keys.
//   4. Ingestion is asynchronous, so this app POLLS the read APIs for up to ~30s.
//
// This app is NOT part of CI — it requires a live server. Run it by hand after
// standing Langfuse up. It exits 0 on success, non-zero on any mismatch.
//
// ── RESILIENCY MODE (no server required) ─────────────────────────────────────
// Run with the argument `resiliency` to prove the opposite case: that an eval
// still PASSES when Langfuse is unreachable. It points the exporter + score
// client at a dead port, runs a scenario, and asserts the run completes (exit 0)
// while every dropped score is surfaced via ScoresFailed + ScoreErrorCallback:
//
//     dotnet run --project src/Examples/AgentFramework/LangfuseConformanceApp -- resiliency
// =============================================================================

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
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

Console.WriteLine("=== Needlr → Langfuse Conformance Check ===");
Console.WriteLine();

// Two modes:
//   (default)      live read-back conformance — requires a running Langfuse.
//   "resiliency"   Langfuse-down resiliency — points at a dead port, needs NO server,
//                  and proves an eval still passes when score uploads fail.
if (args.Length > 0 && string.Equals(args[0], "resiliency", StringComparison.OrdinalIgnoreCase))
{
    return await RunResiliencyCheckAsync();
}

var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");
var host = Environment.GetEnvironmentVariable("LANGFUSE_HOST") ?? "http://localhost:3000";

if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
{
    Console.WriteLine("This conformance check requires a LIVE Langfuse instance.");
    Console.WriteLine("Set the following environment variables and re-run:");
    Console.WriteLine("  LANGFUSE_PUBLIC_KEY=pk-lf-...");
    Console.WriteLine("  LANGFUSE_SECRET_KEY=sk-lf-...");
    Console.WriteLine($"  LANGFUSE_HOST={host}  (default; override for Cloud or a different port)");
    Console.WriteLine();
    Console.WriteLine("Stand up a local instance with: https://langfuse.com/self-hosting/docker-compose");
    return 2;
}

Console.WriteLine($"[setup] Langfuse host: {host}");
Console.WriteLine();

// ── Build a Needlr agent with diagnostics + a credential-free mock LLM ───────
var configuration = new ConfigurationBuilder().Build();

var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .Configure(opts => opts.ChatClientFactory = _ => new MockChatClient())
        .UsingDiagnostics())
    .BuildServiceProvider(configuration);

var loop = serviceProvider.GetRequiredService<IIterativeAgentLoop>();
var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

// ── Start Langfuse export (Host is set, so this is an explicit, enabled target) ─
var options = new LangfuseOptions
{
    PublicKey = publicKey,
    SecretKey = secretKey,
    Host = host,
    ServiceName = "needlr-langfuse-conformance",
    ScoreFailureMode = LangfuseScoreFailureMode.Strict, // a conformance check WANTS hard failures
    DiagnosticsCallback = msg => Console.WriteLine($"[langfuse] {msg}"),
};

using var langfuse = LangfuseTelemetry.Start(options);
if (!langfuse.IsEnabled)
{
    Console.WriteLine("[error] Langfuse export did not enable — check keys/host.");
    return 2;
}

var runId = $"conformance-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
string? traceId;
IReadOnlyList<string> expectedScoreNames;

using (var scenario = langfuse.BeginScenario(
    name: "conformance: cached-summary",
    sessionId: runId,
    tags: ["conformance"]))
{
    traceId = scenario.TraceId;
    Console.WriteLine($"[run] Scenario trace id: {traceId}");

    var loopOptions = new IterativeLoopOptions
    {
        Instructions = "You summarise cached prompt content.",
        PromptFactory = _ => "Summarize the cached prompt content.",
        Tools = [],
        MaxIterations = 1,
        IsComplete = _ => true,
        LoopName = "conformance-demo",
    };

    IAgentRunDiagnostics? diagnostics;
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
        Console.WriteLine("[error] No diagnostics captured.");
        return 2;
    }

    // One-liner: run the evaluators and record every metric as a Langfuse score.
    var inputs = diagnostics.ToEvaluationInputs();
    var results = await scenario.EvaluateAndRecordAsync(
        evaluators:
        [
            new EfficiencyEvaluator(tokenBudget: 200_000),
            new IterationCoherenceEvaluator(maxIterations: 20),
        ],
        messages: inputs.Messages,
        modelResponse: inputs.ModelResponse,
        additionalContext: [new AgentRunDiagnosticsContext(diagnostics)]);

    expectedScoreNames = results
        .SelectMany(r => r.Metrics.Values)
        .Where(HasValue)
        .Select(m => m.Name)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    Console.WriteLine($"[run] Recorded {expectedScoreNames.Count} score(s).");
}

// Scenario disposed (root span ended); flush so the trace is exported now.
langfuse.Flush(TimeSpan.FromSeconds(10));
Console.WriteLine("[run] Flushed telemetry. Verifying read-back from Langfuse...");
Console.WriteLine();

if (string.IsNullOrEmpty(traceId))
{
    Console.WriteLine("[error] No trace id was produced.");
    return 2;
}

using var http = new HttpClient { BaseAddress = new Uri(host.TrimEnd('/') + "/") };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
    "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}")));

var traceFound = await PollAsync(
    description: "trace",
    attempts: 15,
    delay: TimeSpan.FromSeconds(2),
    probe: async () => await GetSucceedsAsync(http, $"api/public/traces/{traceId}"));

var foundScoreNames = await PollForScoresAsync(http, traceId, expectedScoreNames.Count, attempts: 15, delay: TimeSpan.FromSeconds(2));

Console.WriteLine();
var missing = expectedScoreNames.Where(n => !foundScoreNames.Contains(n)).ToList();
var passed = traceFound && missing.Count == 0;

Console.WriteLine($"  trace GET-readable:  {(traceFound ? "PASS" : "FAIL")}");
Console.WriteLine($"  scores GET-readable: {(missing.Count == 0 ? "PASS" : "FAIL")} ({foundScoreNames.Count}/{expectedScoreNames.Count})");
if (missing.Count > 0)
{
    Console.WriteLine($"  missing scores: {string.Join(", ", missing)}");
}

Console.WriteLine();
if (passed)
{
    Console.WriteLine("CONFORMANCE PASSED — trace and all scores are readable from Langfuse.");
    return 0;
}

Console.WriteLine("CONFORMANCE FAILED — see above. (Increase the poll window if your instance is slow.)");
return 1;

// ── Resiliency mode: prove an eval survives Langfuse being unreachable ───────
// Points the exporter and score client at a dead local port (no server needed),
// runs a real scenario, and asserts the eval completes (exit 0) while the score
// failures are surfaced via ScoresFailed + ScoreErrorCallback (never swallowed).
static async Task<int> RunResiliencyCheckAsync()
{
    Console.WriteLine("[mode] Resiliency: simulating Langfuse DOWN (dead port, no server required).");
    Console.WriteLine();

    const string deadHost = "http://127.0.0.1:1";
    var callbackFired = 0;

    var options = new LangfuseOptions
    {
        PublicKey = "pk-lf-resiliency",
        SecretKey = "sk-lf-resiliency",
        Host = deadHost,
        ServiceName = "needlr-langfuse-resiliency",
        ScoreFailureMode = LangfuseScoreFailureMode.NonFatal,
        ScoreErrorCallback = _ => Interlocked.Increment(ref callbackFired),
    };

    var serviceProvider = new Syringe()
        .UsingReflection()
        .UsingAgentFramework(af => af
            .Configure(opts => opts.ChatClientFactory = _ => new MockChatClient())
            .UsingDiagnostics())
        .BuildServiceProvider(new ConfigurationBuilder().Build());

    var loop = serviceProvider.GetRequiredService<IIterativeAgentLoop>();
    var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

    using var langfuse = LangfuseTelemetry.Start(options);
    if (!langfuse.IsEnabled)
    {
        Console.WriteLine("[error] Expected export to be ENABLED (keys + host set).");
        return 1;
    }

    var recordedScores = 0;
    using (var scenario = langfuse.BeginScenario("resiliency: cached-summary", sessionId: "resiliency-run", tags: ["resiliency"]))
    {
        var loopOptions = new IterativeLoopOptions
        {
            Instructions = "You summarise cached prompt content.",
            PromptFactory = _ => "Summarize the cached prompt content.",
            Tools = [],
            MaxIterations = 1,
            IsComplete = _ => true,
            LoopName = "resiliency-demo",
        };

        IAgentRunDiagnostics? diagnostics;
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
            Console.WriteLine("[error] No diagnostics captured.");
            return 1;
        }

        var inputs = diagnostics.ToEvaluationInputs();
        var results = await scenario.EvaluateAndRecordAsync(
            evaluators: [new EfficiencyEvaluator(tokenBudget: 200_000)],
            messages: inputs.Messages,
            modelResponse: inputs.ModelResponse,
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)]);

        recordedScores = results.SelectMany(r => r.Metrics.Values).Count(HasValue);
    }

    langfuse.Flush(TimeSpan.FromSeconds(2));

    // The eval reached this point without throwing — that alone proves NonFatal worked.
    // The failures must still be surfaced (not silently swallowed): counter + callback.
    var resilient =
        recordedScores > 0 &&
        langfuse.ScoresFailed == recordedScores &&
        callbackFired == recordedScores;

    Console.WriteLine();
    Console.WriteLine($"  scores attempted:          {recordedScores}");
    Console.WriteLine($"  ScoresFailed (session):    {langfuse.ScoresFailed}");
    Console.WriteLine($"  ScoreErrorCallback fired:  {callbackFired}");
    Console.WriteLine();

    if (resilient)
    {
        Console.WriteLine("RESILIENCY PASSED — the eval completed despite Langfuse being down,");
        Console.WriteLine("and every dropped score was surfaced (counter + callback), not swallowed.");
        return 0;
    }

    Console.WriteLine("RESILIENCY FAILED — failures were not surfaced as expected.");
    return 1;
}

static bool HasValue(EvaluationMetric metric) => metric switch
{
    NumericMetric nm => nm.Value.HasValue,
    BooleanMetric bm => bm.Value.HasValue,
    StringMetric sm => !string.IsNullOrEmpty(sm.Value),
    _ => false,
};

static async Task<bool> GetSucceedsAsync(HttpClient http, string path)
{
    using var response = await http.GetAsync(path);
    return response.IsSuccessStatusCode;
}

static async Task<bool> PollAsync(string description, int attempts, TimeSpan delay, Func<Task<bool>> probe)
{
    for (var i = 1; i <= attempts; i++)
    {
        if (await probe())
        {
            Console.WriteLine($"[verify] {description} found (attempt {i}).");
            return true;
        }

        await Task.Delay(delay);
    }

    Console.WriteLine($"[verify] {description} NOT found after {attempts} attempts.");
    return false;
}

static async Task<HashSet<string>> PollForScoresAsync(HttpClient http, string traceId, int expectedCount, int attempts, TimeSpan delay)
{
    var found = new HashSet<string>(StringComparer.Ordinal);

    for (var i = 1; i <= attempts; i++)
    {
        found = await GetScoreNamesAsync(http, traceId);
        if (found.Count >= expectedCount)
        {
            Console.WriteLine($"[verify] scores found (attempt {i}): {found.Count}.");
            return found;
        }

        await Task.Delay(delay);
    }

    return found;
}

static async Task<HashSet<string>> GetScoreNamesAsync(HttpClient http, string traceId)
{
    var names = new HashSet<string>(StringComparer.Ordinal);

    using var response = await http.GetAsync($"api/public/v3/scores?traceId={Uri.EscapeDataString(traceId)}&limit=100");
    if (!response.IsSuccessStatusCode)
    {
        return names;
    }

    using var stream = await response.Content.ReadAsStreamAsync();
    using var json = await JsonDocument.ParseAsync(stream);
    if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
    {
        return names;
    }

    foreach (var score in data.EnumerateArray())
    {
        // Defensive: filter client-side by traceId in case the server ignores the query param.
        if (score.TryGetProperty("traceId", out var tid) && tid.GetString() != traceId)
        {
            continue;
        }

        if (score.TryGetProperty("name", out var name) && name.GetString() is { } n)
        {
            names.Add(n);
        }
    }

    return names;
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
