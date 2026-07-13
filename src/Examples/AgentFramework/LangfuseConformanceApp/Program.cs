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
// still PASSES when Langfuse is unreachable. It proves cancellation propagates,
// experiment-item callbacks retain their scenario context, unrelated baggage is
// not exported, final shutdown is bounded, and every failed score is surfaced via
// structured publication health + ScoreErrorCallback:
//
//     dotnet run --project src/Examples/AgentFramework/LangfuseConformanceApp -- resiliency
//
// Run with `dependency-injection` to prove that AddNeedlrLangfuse registers a
// complete non-owning facade backed by one host-owned OpenTelemetry pipeline.
// =============================================================================

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

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
//   "resiliency"   Langfuse-down lifecycle and resiliency checks — needs NO server.
if (args.Length > 0 && string.Equals(args[0], "resiliency", StringComparison.OrdinalIgnoreCase))
{
    return await RunResiliencyCheckAsync();
}

if (args.Length > 0 && string.Equals(args[0], "dependency-injection", StringComparison.OrdinalIgnoreCase))
{
    return RunDependencyInjectionCheck();
}

// "experiments" mode exercises the dataset/experiment, score-config, comment,
// trace-context, and session-score features end to end against a LIVE Langfuse,
// then reads each one back through the public API to prove it landed.
if (args.Length > 0 && string.Equals(args[0], "experiments", StringComparison.OrdinalIgnoreCase))
{
    return await RunExperimentsCheckAsync();
}

// "prompt-linking" mode proves that scenario.SetPrompt stamps a generation with the
// managed-prompt name/version, readable back on the observation.
if (args.Length > 0 && string.Equals(args[0], "prompt-linking", StringComparison.OrdinalIgnoreCase))
{
    return await RunPromptLinkingCheckAsync();
}

// "metrics" mode proves read-back: record a uniquely-named numeric score, then query its
// average back through the Metrics API (the loop a CI quality gate would run).
if (args.Length > 0 && string.Equals(args[0], "metrics", StringComparison.OrdinalIgnoreCase))
{
    return await RunMetricsCheckAsync();
}

// "model-pricing" mode proves that registering a model price lets Langfuse compute cost for a
// generation whose model it would not otherwise price.
if (args.Length > 0 && string.Equals(args[0], "model-pricing", StringComparison.OrdinalIgnoreCase))
{
    return await RunModelPricingCheckAsync();
}

// "prompt-fetching" mode proves the follow-up: create + fetch a managed prompt through Needlr,
// use its text, and auto-link generations to the fetched version.
if (args.Length > 0 && string.Equals(args[0], "prompt-fetching", StringComparison.OrdinalIgnoreCase))
{
    return await RunPromptFetchingCheckAsync();
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
        additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
        scoreOptions: new LangfuseEvaluationScoreOptions
        {
            ScoreIdProvider = metric => $"{runId}:{metric.Name}",
        });

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

// ── Resiliency mode: prove safe behavior when Langfuse is unreachable ────────
// Exercises cancellation, trace-context propagation, score failure reporting,
// and bounded final shutdown against a dead local port (no server required).
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
    using var contextProbeSource = new ActivitySource("Needlr.Langfuse.Conformance.ContextProbe");
    options.AdditionalActivitySources.Add(contextProbeSource.Name);

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

    var cancellationPropagated = false;
    using (var cancellation = new CancellationTokenSource())
    {
        cancellation.Cancel();
        try
        {
            await langfuse.Datasets.EnsureDatasetAsync("cancelled-operation", cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellation.Token)
        {
            cancellationPropagated = true;
        }
    }

    var recordedScores = 0;
    var contextPropagationSafe = false;
    using (var externalContext = new Activity("resiliency-parent").Start())
    {
        externalContext.SetBaggage("authorization", "must-not-export");
        using var scenario = langfuse.BeginScenario(
            "resiliency: cached-summary",
            sessionId: "resiliency-run",
            userId: "resiliency-user",
            tags: ["resiliency", "offline"],
            metadata: new Dictionary<string, string> { ["suite"] = "conformance" });
        scenario.SetVersion("resiliency-v1");

        using var contextProbe = contextProbeSource.StartActivity("context-probe");
        if (contextProbe is null)
        {
            Console.WriteLine("[error] Context probe activity was not sampled.");
            return 1;
        }

        contextPropagationSafe =
            Equals(contextProbe.GetTagItem("langfuse.trace.name"), "resiliency: cached-summary") &&
            Equals(contextProbe.GetTagItem("session.id"), "resiliency-run") &&
            Equals(contextProbe.GetTagItem("user.id"), "resiliency-user") &&
            Equals(contextProbe.GetTagItem("langfuse.version"), "resiliency-v1") &&
            Equals(contextProbe.GetTagItem("langfuse.trace.metadata.suite"), "conformance") &&
            contextProbe.GetTagItem("langfuse.trace.tags") is string[] tags &&
            tags.SequenceEqual(["resiliency", "offline"]) &&
            contextProbe.GetTagItem("authorization") is null;

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
            additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
            scoreOptions: new LangfuseEvaluationScoreOptions
            {
                ScoreIdProvider = metric => $"resiliency:{metric.Name}",
            });

        recordedScores = results.SelectMany(r => r.Metrics.Values).Count(HasValue);
    }

    var experimentItemContextSafe = false;
    var experimentItemContextRestored = false;
    var experimentItemLinkStatus = LangfuseExperimentItemLinkStatus.Linked;
    using (var externalContext = new Activity("resiliency-experiment-parent").Start())
    {
        var experiment = langfuse.BeginExperimentRun("resiliency-dataset", "resiliency-run");
        var itemResult = await experiment.RunItemAsync(
            "resiliency-item",
            async (scenario, cancellationToken) =>
            {
                var scenarioActivity = scenario.Activity;
                await Task.Yield();
                using var child = contextProbeSource.StartActivity("experiment-item-context-probe");
                experimentItemContextSafe =
                    scenarioActivity is not null &&
                    ReferenceEquals(Activity.Current, child) &&
                    child is not null &&
                    child.TraceId == scenarioActivity.TraceId &&
                    child.ParentSpanId == scenarioActivity.SpanId;
                cancellationToken.ThrowIfCancellationRequested();
                return scenario.TraceId;
            },
            new LangfuseExperimentItemOptions
            {
                ScenarioName = "resiliency: experiment-item",
                Tags = ["resiliency", "experiment"],
            },
            CancellationToken.None);

        experimentItemContextRestored = ReferenceEquals(Activity.Current, externalContext);
        experimentItemLinkStatus = itemResult.Link.Status;
    }

    var shutdownTimeout = TimeSpan.FromSeconds(2);
    var shutdownStopwatch = Stopwatch.StartNew();
    var shutdown = langfuse.Shutdown(shutdownTimeout);
    shutdownStopwatch.Stop();
    var publication = langfuse.PublicationHealth.GetSnapshot();
    var shutdownBounded =
        shutdownStopwatch.Elapsed <= shutdownTimeout + TimeSpan.FromSeconds(3);

    var resilient =
        cancellationPropagated &&
        contextPropagationSafe &&
        experimentItemContextSafe &&
        experimentItemContextRestored &&
        experimentItemLinkStatus == LangfuseExperimentItemLinkStatus.Failed &&
        recordedScores > 0 &&
        publication.ScoreUploads.Failed == recordedScores &&
        publication.ItemLinks.Failed == 1 &&
        publication.Retries.Total > 0 &&
        callbackFired == recordedScores &&
        shutdown.IsFinal &&
        shutdownBounded &&
        shutdown.Metrics == LangfuseProviderShutdownStatus.NotConfigured;

    Console.WriteLine();
    Console.WriteLine($"  caller cancellation:       {(cancellationPropagated ? "propagated" : "FAILED")}");
    Console.WriteLine($"  trace context allowlist:   {(contextPropagationSafe ? "safe" : "FAILED")}");
    Console.WriteLine($"  experiment callback scope: {(experimentItemContextSafe ? "safe" : "FAILED")}");
    Console.WriteLine($"  experiment scope restore:  {(experimentItemContextRestored ? "restored" : "FAILED")}");
    Console.WriteLine($"  dataset link status:       {experimentItemLinkStatus}");
    Console.WriteLine($"  scores attempted:          {recordedScores}");
    Console.WriteLine($"  score upload failures:     {publication.ScoreUploads.Failed}");
    Console.WriteLine($"  item link failures:        {publication.ItemLinks.Failed}");
    Console.WriteLine($"  REST retries:              {publication.Retries.Total}");
    Console.WriteLine($"  trace export failures:     {publication.TraceExport.Failed}");
    Console.WriteLine($"  trace queue drops:         {publication.TraceExport.Dropped}");
    Console.WriteLine($"  ScoreErrorCallback fired:  {callbackFired}");
    Console.WriteLine($"  trace shutdown:            {shutdown.Traces}");
    Console.WriteLine($"  metric shutdown:           {shutdown.Metrics}");
    Console.WriteLine($"  drain health:              {publication.Drain.Status}");
    Console.WriteLine($"  shutdown elapsed:          {shutdownStopwatch.Elapsed.TotalMilliseconds:F0} ms");
    Console.WriteLine();

    if (resilient)
    {
        Console.WriteLine("RESILIENCY PASSED — cancellation propagated, experiment item context was");
        Console.WriteLine("preserved, trace context was allowlisted, publication failures were surfaced,");
        Console.WriteLine("and final shutdown was bounded.");
        return 0;
    }

    Console.WriteLine("RESILIENCY FAILED — failures were not surfaced as expected.");
    return 1;
}

static int RunDependencyInjectionCheck()
{
    Console.WriteLine("[mode] Dependency injection: complete non-owning Langfuse facade.");
    Console.WriteLine();

    var services = new ServiceCollection();
    services.AddNeedlrLangfuse(options =>
    {
        options.PublicKey = "pk-lf-di-conformance";
        options.SecretKey = "sk-lf-di-conformance";
        options.Host = "http://127.0.0.1:1";
        options.ServiceName = "needlr-langfuse-di-conformance";
    });

    var provider = services.BuildServiceProvider();
    var passed = false;
    try
    {
        var client = provider.GetRequiredService<ILangfuseClient>();
        var tracerProviders = provider.GetServices<TracerProvider>().ToArray();
        using var scenario = client.BeginScenario(
            "di: hosted-scenario",
            sessionId: "di-run",
            tags: ["dependency-injection"]);
        var experiment = client.BeginExperimentRun("di-dataset", "di-experiment");

        var identitiesMatch =
            ReferenceEquals(client.Scores, provider.GetRequiredService<ILangfuseScoreClient>()) &&
            ReferenceEquals(client.Datasets, provider.GetRequiredService<ILangfuseDatasetClient>()) &&
            ReferenceEquals(client.ScoreConfigs, provider.GetRequiredService<ILangfuseScoreConfigClient>()) &&
            ReferenceEquals(client.Metrics, provider.GetRequiredService<ILangfuseMetricsClient>()) &&
            ReferenceEquals(client.Models, provider.GetRequiredService<ILangfuseModelClient>()) &&
            ReferenceEquals(client.Prompts, provider.GetRequiredService<ILangfusePromptClient>());

        passed =
            client.IsEnabled &&
            client is not IDisposable &&
            provider.GetService<ILangfuseSession>() is null &&
            tracerProviders.Length == 1 &&
            scenario.TraceId is { Length: > 0 } &&
            experiment.DatasetName == "di-dataset" &&
            experiment.RunName == "di-experiment" &&
            identitiesMatch;

        Console.WriteLine($"  facade enabled:            {client.IsEnabled}");
        Console.WriteLine($"  facade owns lifecycle:     {client is IDisposable}");
        Console.WriteLine($"  ILangfuseSession in DI:    {provider.GetService<ILangfuseSession>() is not null}");
        Console.WriteLine($"  TracerProvider count:      {tracerProviders.Length}");
        Console.WriteLine($"  scenario trace id:         {scenario.TraceId}");
        Console.WriteLine($"  experiment:                {experiment.DatasetName}/{experiment.RunName}");
        Console.WriteLine($"  specialized identities:    {(identitiesMatch ? "shared" : "MISMATCH")}");
    }
    finally
    {
        var disposalStopwatch = Stopwatch.StartNew();
        provider.Dispose();
        disposalStopwatch.Stop();
        Console.WriteLine($"  provider disposal:         {disposalStopwatch.Elapsed.TotalMilliseconds:F0} ms");
    }

    Console.WriteLine();
    if (passed)
    {
        Console.WriteLine("DEPENDENCY INJECTION PASSED — one host telemetry pipeline owns lifecycle,");
        Console.WriteLine("while ILangfuseClient provides the complete non-owning evaluation surface.");
        return 0;
    }

    Console.WriteLine("DEPENDENCY INJECTION FAILED — hosted facade ownership or identity was incorrect.");
    return 1;
}

// ── Experiments mode: prove every Wave-1..3 capability against a LIVE Langfuse ─
// Exercises datasets + experiment runs (dataset-run-items), score configs,
// comments, trace context (environment/release/public/input/output), and
// session scoring — then reads each back through the public API to prove it
// actually landed.
static async Task<int> RunExperimentsCheckAsync()
{
    var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
    var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");
    var host = Environment.GetEnvironmentVariable("LANGFUSE_HOST") ?? "http://localhost:3000";

    if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
    {
        Console.WriteLine("This check requires a LIVE Langfuse instance.");
        Console.WriteLine("Set LANGFUSE_PUBLIC_KEY / LANGFUSE_SECRET_KEY (and optionally LANGFUSE_HOST) and re-run.");
        return 2;
    }

    Console.WriteLine($"[setup] Langfuse host: {host}");

    var runId = $"needlr-exp-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
    const string datasetName = "needlr-conformance-dataset";
    const string configName = "needlr_conformance_score";
    var sessionId = $"{runId}-session";

    var options = new LangfuseOptions
    {
        PublicKey = publicKey,
        SecretKey = secretKey,
        Host = host,
        ServiceName = "needlr-langfuse-experiments",
        Environment = "needlr-conformance",
        Release = runId,
        ScoreFailureMode = LangfuseScoreFailureMode.Strict,
        DiagnosticsCallback = msg => Console.WriteLine($"[langfuse] {msg}"),
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
        Console.WriteLine("[error] Langfuse export did not enable — check keys/host.");
        return 2;
    }

    // P2 — score config (idempotent)
    await langfuse.ScoreConfigs.EnsureScoreConfigAsync(new LangfuseScoreConfig
    {
        Name = configName,
        DataType = LangfuseScoreDataType.Numeric,
        MinValue = 0,
        MaxValue = 1,
        Description = "Needlr conformance numeric score config.",
    });
    Console.WriteLine($"[setup] Ensured score config '{configName}'.");

    // P1 — dataset + items
    await langfuse.Datasets.EnsureDatasetAsync(datasetName, "Needlr conformance dataset.");
    var items = new[] { "case-cached", "case-fresh" };
    foreach (var id in items)
    {
        await langfuse.Datasets.UpsertItemAsync(new LangfuseDatasetItem
        {
            DatasetName = datasetName,
            Id = id,
            Input = new { prompt = $"Summarize content for {id}." },
            ExpectedOutput = "a concise summary",
        });
    }
    Console.WriteLine($"[setup] Ensured dataset '{datasetName}' with {items.Length} items.");

    // P1 — experiment run: a trace per item, linked as a dataset-run-item
    var run = langfuse.BeginExperimentRun(
        datasetName,
        runId,
        new LangfuseExperimentRunOptions
        {
            Description = "Needlr conformance run.",
            Metadata = new
            {
                suite = "needlr-conformance",
                candidate = new
                {
                    name = "mock-agent",
                    version = "prompt-v1",
                },
            },
        });
    var itemTraceIds = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var id in items)
    {
        var itemResult = await run.RunItemAsync(
            id,
            async (scenario, cancellationToken) =>
            {
                if (!ReferenceEquals(Activity.Current, scenario.Activity))
                {
                    throw new InvalidOperationException(
                        $"Experiment item '{id}' did not execute inside its scenario activity.");
                }

                scenario.SetInput(new { prompt = $"Summarize content for {id}." });
                scenario.SetVersion("prompt-v1");
                scenario.SetTracePublic();

                var diagnostics = await RunMockAsync(
                    loop,
                    diagnosticsAccessor,
                    $"summary-{id}",
                    cancellationToken: cancellationToken);
                if (diagnostics is null)
                {
                    return null;
                }

                scenario.SetOutput("Mock summary of the cached prompt content.");

                var inputs = diagnostics.ToEvaluationInputs();
                await scenario.EvaluateAndRecordAsync(
                    evaluators:
                    [
                        new EfficiencyEvaluator(tokenBudget: 200_000),
                        new IterationCoherenceEvaluator(maxIterations: 20),
                    ],
                    messages: inputs.Messages,
                    modelResponse: inputs.ModelResponse,
                    additionalContext: [new AgentRunDiagnosticsContext(diagnostics)],
                    cancellationToken: cancellationToken);
                return scenario.TraceId;
            },
            new LangfuseExperimentItemOptions
            {
                ScenarioName = $"exp: {id}",
                Tags = ["conformance", "experiment"],
                LinkFailureMode = LangfuseExperimentItemLinkFailureMode.Strict,
            },
            CancellationToken.None);

        if (itemResult.Link.Status != LangfuseExperimentItemLinkStatus.Linked)
        {
            Console.WriteLine($"[error] Item '{id}' link status was {itemResult.Link.Status}.");
            return 2;
        }

        var traceId = itemResult.TraceId;
        if (string.IsNullOrEmpty(traceId)
            || string.IsNullOrEmpty(itemResult.Value)
            || string.IsNullOrEmpty(itemResult.Link.DatasetRunId))
        {
            Console.WriteLine($"[error] Missing trace or dataset-run identity for item '{id}'.");
            return 2;
        }

        if (!string.Equals(itemResult.Link.DatasetRunId, run.DatasetRunId, StringComparison.Ordinal))
        {
            Console.WriteLine($"[error] Item '{id}' disagreed with the run's dataset-run id.");
            return 2;
        }

        itemTraceIds[id] = traceId;
    }
    if (run.IdentityStatus != LangfuseDatasetRunIdentityStatus.Resolved
        || string.IsNullOrWhiteSpace(run.DatasetRunId))
    {
        Console.WriteLine($"[error] Dataset-run identity was {run.IdentityStatus}.");
        return 2;
    }

    Console.WriteLine(
        $"[run] Linked {itemTraceIds.Count} item(s) to dataset run {run.DatasetRunId}.");

    var numericRunScore = await run.RecordScoreAsync(
        configName,
        1.0,
        new LangfuseScoreOptions
        {
            Id = $"{runId}:{configName}",
            Comment = "All conformance items linked.",
        });
    var booleanRunScore = await run.RecordScoreAsync(
        "needlr_run_passed",
        true,
        new LangfuseScoreOptions
        {
            Id = $"{runId}:needlr_run_passed",
            Comment = "Every required run check passed before read-back.",
        });
    var categoricalRunScore = await run.RecordScoreAsync(
        "needlr_run_verdict",
        "passed",
        new LangfuseScoreOptions { Id = $"{runId}:needlr_run_verdict" });
    var evaluationRunScores = await run.RecordEvaluationAsync(
        new EvaluationResult(new NumericMetric("needlr_run_item_count", itemTraceIds.Count)),
        new LangfuseEvaluationScoreOptions
        {
            ScoreIdProvider = metric => $"{runId}:{metric.Name}",
        });
    LangfuseExperimentRunScoreResult[] runScoreResults =
    [
        numericRunScore,
        booleanRunScore,
        categoricalRunScore,
        .. evaluationRunScores,
    ];
    if (runScoreResults.Any(result => result.Status != LangfuseExperimentRunScoreStatus.Accepted))
    {
        Console.WriteLine("[error] One or more dataset-run scores were not accepted.");
        return 2;
    }

    var publicationSnapshot = run.GetPublicationSnapshot();
    Console.WriteLine(
        $"[run] API publication {publicationSnapshot.ApiPublicationStatus}: " +
        $"{publicationSnapshot.ItemLinks.Linked} link(s), " +
        $"{publicationSnapshot.RunScores.Accepted} run score(s).");

    // P3 — session-level scoring (separate scenario carrying a session id)
    using (var sessionScenario = langfuse.BeginScenario("exp: session-scored", sessionId: sessionId, tags: ["conformance"]))
    {
        await RunMockAsync(loop, diagnosticsAccessor, "session-trace");
        await sessionScenario.RecordSessionScoreAsync(
            "session_resolved",
            true,
            new LangfuseScoreOptions
            {
                Id = $"{runId}:session_resolved",
                Comment = "Whole conversation resolved.",
            });
    }
    Console.WriteLine($"[run] Recorded a session score on session '{sessionId}'.");

    langfuse.Flush(TimeSpan.FromSeconds(10));
    Console.WriteLine("[run] Flushed telemetry. Verifying read-back from Langfuse...");
    Console.WriteLine();

    using var http = new HttpClient { BaseAddress = new Uri(host.TrimEnd('/') + "/") };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}")));

    var firstTraceId = itemTraceIds[items[0]];

    // P11 — comments require the target trace to already exist, so (unlike scores) they are a
    // POST-FLUSH operation: wait for the trace to be ingested, then attach the comment by id.
    var commentTraceReady = await PollAsync("trace ingestion (for comment)", 15, TimeSpan.FromSeconds(2),
        async () => await GetRawAsync(http, $"api/public/traces/{Uri.EscapeDataString(firstTraceId)}") is not null);
    if (commentTraceReady)
    {
        await langfuse.AddTraceCommentAsync(firstTraceId, $"Needlr conformance run {runId} — item {items[0]}.");
    }

    // CHECK — P1: the experiment run read-back references every item's trace and requested metadata.
    var runReadBack = await PollAsync("experiment run items", 15, TimeSpan.FromSeconds(2), async () =>
    {
        var body = await GetRawAsync(http, $"api/public/datasets/{Uri.EscapeDataString(datasetName)}/runs/{Uri.EscapeDataString(runId)}");
        if (body is null)
        {
            return false;
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        return string.Equals(root.GetProperty("id").GetString(), run.DatasetRunId, StringComparison.Ordinal)
            && string.Equals(root.GetProperty("description").GetString(), "Needlr conformance run.", StringComparison.Ordinal)
            && string.Equals(root.GetProperty("metadata").GetProperty("suite").GetString(), "needlr-conformance", StringComparison.Ordinal)
            && string.Equals(
                root.GetProperty("metadata").GetProperty("candidate").GetProperty("version").GetString(),
                "prompt-v1",
                StringComparison.Ordinal)
            && itemTraceIds.Values.All(body.Contains);
    });

    var expectedRunScoreNames = runScoreResults
        .Select(result => result.Name)
        .ToHashSet(StringComparer.Ordinal);
    var runScoresReadBack = await PollAsync("dataset run scores", 15, TimeSpan.FromSeconds(2), async () =>
    {
        var names = await GetScoreNamesByQueryAsync(
            http,
            $"experimentId={Uri.EscapeDataString(run.DatasetRunId!)}");
        return expectedRunScoreNames.IsSubsetOf(names);
    });

    // CHECK — P2: the score config is readable by name.
    var configReadBack = await PollAsync("score config", 10, TimeSpan.FromSeconds(2), async () =>
    {
        var body = await GetRawAsync(http, "api/public/score-configs?page=1&limit=100");
        return body is not null && body.Contains($"\"{configName}\"", StringComparison.Ordinal);
    });

    // CHECK — P11: the trace comment is readable.
    var commentReadBack = await PollAsync("trace comment", 15, TimeSpan.FromSeconds(2), async () =>
    {
        var body = await GetRawAsync(http, $"api/public/comments?objectType=TRACE&objectId={Uri.EscapeDataString(firstTraceId)}");
        return body is not null && body.Contains(runId, StringComparison.Ordinal);
    });

    // CHECK — P4/P6: trace context (environment, release, public, input, output).
    var (contextReadBack, contextDetail) = await PollTraceContextAsync(http, firstTraceId, "needlr-conformance", runId);

    // CHECK — scores: trace-level eval scores landed.
    var traceScores = await PollForScoresAsync(http, firstTraceId, expectedCount: 1, attempts: 15, delay: TimeSpan.FromSeconds(2));

    // CHECK — P3: the session score is readable by session id.
    var sessionScoreReadBack = await PollAsync("session score", 15, TimeSpan.FromSeconds(2), async () =>
    {
        var names = await GetScoreNamesByQueryAsync(http, $"sessionId={Uri.EscapeDataString(sessionId)}");
        return names.Contains("session_resolved");
    });

    Console.WriteLine();
    Console.WriteLine("── Results ──────────────────────────────────────────────");
    Console.WriteLine($"  P1 experiment run-items linked:   {Mark(runReadBack)}");
    Console.WriteLine($"  P1 dataset-run scores readable:   {Mark(runScoresReadBack)}");
    Console.WriteLine($"  P1 API publication snapshot:      {publicationSnapshot.ApiPublicationStatus}");
    Console.WriteLine($"  P2 score config readable:         {Mark(configReadBack)}");
    Console.WriteLine($"  P11 trace comment readable:       {Mark(commentReadBack)}");
    Console.WriteLine($"  P4/P6 trace context:              {Mark(contextReadBack)}  {contextDetail}");
    Console.WriteLine($"  trace-level eval scores:          {Mark(traceScores.Count > 0)} ({traceScores.Count})");
    Console.WriteLine($"  P3 session score readable:        {Mark(sessionScoreReadBack)}");
    Console.WriteLine();

    var passed = runReadBack && runScoresReadBack && configReadBack && commentReadBack && contextReadBack
        && publicationSnapshot.ApiPublicationStatus == LangfuseExperimentApiPublicationStatus.Complete
        && traceScores.Count > 0 && sessionScoreReadBack;

    if (passed)
    {
        Console.WriteLine("EXPERIMENTS CONFORMANCE PASSED — every new capability is GET-readable from Langfuse.");
        return 0;
    }

    Console.WriteLine("EXPERIMENTS CONFORMANCE FAILED — see above. (Increase the poll window if your instance is slow.)");
    return 1;
}

static string Mark(bool ok) => ok ? "PASS" : "FAIL";

// ── Prompt-fetching mode: create + fetch a managed prompt, then auto-link ────
static async Task<int> RunPromptFetchingCheckAsync()
{
    var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
    var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");
    var host = Environment.GetEnvironmentVariable("LANGFUSE_HOST") ?? "http://localhost:3000";
    if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
    {
        Console.WriteLine("This check requires a LIVE Langfuse instance (set LANGFUSE_PUBLIC_KEY / LANGFUSE_SECRET_KEY).");
        return 2;
    }

    Console.WriteLine($"[setup] Langfuse host: {host}");

    var promptName = $"needlr-fetch-prompt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    const string promptText = "You are a concise summariser.";
    var runId = $"needlr-fetch-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

    var options = new LangfuseOptions
    {
        PublicKey = publicKey,
        SecretKey = secretKey,
        Host = host,
        ServiceName = "needlr-langfuse-prompt-fetching",
        Environment = "needlr-conformance",
        ScoreFailureMode = LangfuseScoreFailureMode.Strict,
        DiagnosticsCallback = msg => Console.WriteLine($"[langfuse] {msg}"),
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
        Console.WriteLine("[error] Langfuse export did not enable — check keys/host.");
        return 2;
    }

    // Create the managed prompt via Needlr's prompt client, then fetch it back.
    var created = await langfuse.Prompts.CreateTextPromptAsync(promptName, promptText, ["production"]);
    Console.WriteLine($"[setup] Created prompt '{created.Name}' v{created.Version}.");

    var fetched = await langfuse.Prompts.GetPromptAsync(promptName, label: "production");
    if (fetched is null)
    {
        Console.WriteLine("[error] Fetch returned null.");
        return 1;
    }

    var fetchOk = string.Equals(fetched.Text, promptText, StringComparison.Ordinal)
        && string.Equals(fetched.Name, promptName, StringComparison.Ordinal);
    Console.WriteLine($"[run] Fetched prompt '{fetched.Name}' v{fetched.Version}, text='{fetched.Text}'.");

    string? traceId;
    using (var scenario = langfuse.BeginScenario("prompt-fetching: summary", sessionId: runId, tags: ["conformance"]))
    {
        scenario.SetPrompt(fetched); // auto-link to the fetched version
        traceId = scenario.TraceId;
        await RunMockAsync(loop, diagnosticsAccessor, "prompt-fetching", instructions: fetched.Text ?? promptText);
    }

    langfuse.Flush(TimeSpan.FromSeconds(10));
    Console.WriteLine("[run] Flushed. Verifying the generation links to the fetched prompt...");
    Console.WriteLine();

    if (string.IsNullOrEmpty(traceId))
    {
        Console.WriteLine("[error] No trace id was produced.");
        return 2;
    }

    using var http = new HttpClient { BaseAddress = new Uri(host.TrimEnd('/') + "/") };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}")));

    var detail = string.Empty;
    var linked = await PollAsync("prompt-linked generation", 15, TimeSpan.FromSeconds(2), async () =>
    {
        var (ok, d) = await CheckGenerationPromptAsync(http, traceId, promptName, fetched.Version);
        detail = d;
        return ok;
    });

    Console.WriteLine();
    Console.WriteLine($"  prompt created + fetched via Needlr: {Mark(fetchOk)}");
    Console.WriteLine($"  generation linked to fetched version: {Mark(linked)}  {detail}");
    Console.WriteLine();

    if (fetchOk && linked)
    {
        Console.WriteLine("PROMPT FETCHING PASSED — created + fetched a managed prompt and auto-linked the generation.");
        return 0;
    }

    Console.WriteLine("PROMPT FETCHING FAILED — see above.");
    return 1;
}

// ── Model-pricing mode: prove a registered price yields a computed cost ──────
static async Task<int> RunModelPricingCheckAsync()
{
    var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
    var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");
    var host = Environment.GetEnvironmentVariable("LANGFUSE_HOST") ?? "http://localhost:3000";
    if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
    {
        Console.WriteLine("This check requires a LIVE Langfuse instance (set LANGFUSE_PUBLIC_KEY / LANGFUSE_SECRET_KEY).");
        return 2;
    }

    Console.WriteLine($"[setup] Langfuse host: {host}");

    // Unique model name so the registered price is this run's, and a model Langfuse cannot price by default.
    var modelId = $"needlr-mock-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    var runId = $"needlr-pricing-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

    var options = new LangfuseOptions
    {
        PublicKey = publicKey,
        SecretKey = secretKey,
        Host = host,
        ServiceName = "needlr-langfuse-model-pricing",
        Environment = "needlr-conformance",
        ScoreFailureMode = LangfuseScoreFailureMode.Strict,
        DiagnosticsCallback = msg => Console.WriteLine($"[langfuse] {msg}"),
    };

    var serviceProvider = new Syringe()
        .UsingReflection()
        .UsingAgentFramework(af => af
            .Configure(opts => opts.ChatClientFactory = _ => new MockChatClient(modelId))
            .UsingDiagnostics())
        .BuildServiceProvider(new ConfigurationBuilder().Build());
    var loop = serviceProvider.GetRequiredService<IIterativeAgentLoop>();
    var diagnosticsAccessor = serviceProvider.GetRequiredService<IAgentDiagnosticsAccessor>();

    using var langfuse = LangfuseTelemetry.Start(options);
    if (!langfuse.IsEnabled)
    {
        Console.WriteLine("[error] Langfuse export did not enable — check keys/host.");
        return 2;
    }

    // Register the price BEFORE the generation is ingested so Langfuse computes cost at ingestion.
    await langfuse.Models.EnsureModelPriceAsync(new LangfuseModelPrice
    {
        ModelName = modelId,
        MatchPattern = $"(?i)^{modelId}$",
        InputPrice = 0.000001,
        OutputPrice = 0.000002,
    });
    Console.WriteLine($"[setup] Registered price for model '{modelId}' (input 1e-6, output 2e-6 / token).");

    string? traceId;
    using (var scenario = langfuse.BeginScenario("model-pricing: priced-run", sessionId: runId, tags: ["conformance"]))
    {
        traceId = scenario.TraceId;
        await RunMockAsync(loop, diagnosticsAccessor, "model-pricing");
        Console.WriteLine($"[run] trace {traceId}, generation model '{modelId}'.");
    }

    langfuse.Flush(TimeSpan.FromSeconds(10));
    Console.WriteLine("[run] Flushed. Verifying the generation has a computed cost...");
    Console.WriteLine();

    if (string.IsNullOrEmpty(traceId))
    {
        Console.WriteLine("[error] No trace id was produced.");
        return 2;
    }

    using var http = new HttpClient { BaseAddress = new Uri(host.TrimEnd('/') + "/") };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}")));

    var detail = string.Empty;
    var priced = await PollAsync("generation cost", 20, TimeSpan.FromSeconds(3), async () =>
    {
        var (ok, d) = await CheckGenerationCostAsync(http, traceId);
        detail = d;
        return ok;
    });

    Console.WriteLine();
    Console.WriteLine($"  generation cost computed: {Mark(priced)}  {detail}");
    Console.WriteLine();

    if (priced)
    {
        Console.WriteLine("MODEL PRICING PASSED — Langfuse computed a cost from the registered price.");
        return 0;
    }

    Console.WriteLine("MODEL PRICING FAILED — see above.");
    return 1;
}

static async Task<(bool Ok, string Detail)> CheckGenerationCostAsync(HttpClient http, string traceId)
{
    var body = await GetRawAsync(http, $"api/public/observations?traceId={Uri.EscapeDataString(traceId)}&limit=50");
    if (body is null)
    {
        return (false, "(observations not found)");
    }

    using var json = JsonDocument.Parse(body);
    if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
    {
        return (false, "(no data)");
    }

    foreach (var obs in data.EnumerateArray())
    {
        if ((obs.TryGetProperty("type", out var t) ? t.GetString() : null) != "GENERATION")
        {
            continue;
        }

        var cost = ReadCost(obs);
        return cost is { } c && c > 0
            ? (true, $"(totalCost={c.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)})")
            : (false, $"(generation found, cost={(cost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<none>")})");
    }

    return (false, "(no GENERATION observation yet)");
}

static double? ReadCost(JsonElement observation)
{
    foreach (var key in new[] { "calculatedTotalCost", "totalCost" })
    {
        if (observation.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number)
        {
            return v.GetDouble();
        }
    }

    if (observation.TryGetProperty("costDetails", out var cd)
        && cd.ValueKind == JsonValueKind.Object
        && cd.TryGetProperty("total", out var total)
        && total.ValueKind == JsonValueKind.Number)
    {
        return total.GetDouble();
    }

    return null;
}

// ── Metrics mode: prove the Metrics API read-back loop end to end ────────────
static async Task<int> RunMetricsCheckAsync()
{
    var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
    var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");
    var host = Environment.GetEnvironmentVariable("LANGFUSE_HOST") ?? "http://localhost:3000";
    if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
    {
        Console.WriteLine("This check requires a LIVE Langfuse instance (set LANGFUSE_PUBLIC_KEY / LANGFUSE_SECRET_KEY).");
        return 2;
    }

    Console.WriteLine($"[setup] Langfuse host: {host}");

    // A unique score name makes the average deterministic (only this run's score matches).
    var scoreName = $"needlr_metric_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    const double scoreValue = 0.42;
    var runId = $"needlr-metrics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

    var options = new LangfuseOptions
    {
        PublicKey = publicKey,
        SecretKey = secretKey,
        Host = host,
        ServiceName = "needlr-langfuse-metrics",
        Environment = "needlr-metrics",
        ScoreFailureMode = LangfuseScoreFailureMode.Strict,
        DiagnosticsCallback = msg => Console.WriteLine($"[langfuse] {msg}"),
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
        Console.WriteLine("[error] Langfuse export did not enable — check keys/host.");
        return 2;
    }

    using (var scenario = langfuse.BeginScenario("metrics: scored-run", sessionId: runId, tags: ["conformance"]))
    {
        await RunMockAsync(loop, diagnosticsAccessor, "metrics");
        await scenario.RecordScoreAsync(
            scoreName,
            scoreValue,
            new LangfuseScoreOptions
            {
                Id = $"{runId}:{scoreName}",
                Comment = "metrics conformance",
            });
        Console.WriteLine($"[run] Recorded score '{scoreName}' = {scoreValue} on trace {scenario.TraceId}.");
    }

    langfuse.Flush(TimeSpan.FromSeconds(10));
    Console.WriteLine("[run] Flushed. Querying the Metrics API for the average (allowing for ingestion lag)...");
    Console.WriteLine();

    var from = DateTimeOffset.UtcNow.AddHours(-1);
    var to = DateTimeOffset.UtcNow.AddHours(1);

    double? average = null;
    for (var i = 1; i <= 30; i++)
    {
        average = await langfuse.Metrics.GetScoreAverageAsync(scoreName, from, to);
        if (average is { } a && Math.Abs(a - scoreValue) < 0.0001)
        {
            Console.WriteLine($"[verify] metrics average matched (attempt {i}): {a}.");
            break;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
    }

    Console.WriteLine();
    var passed = average is { } value && Math.Abs(value - scoreValue) < 0.0001;
    Console.WriteLine($"  metrics avg == recorded score: {Mark(passed)}  (expected {scoreValue}, got {(average.HasValue ? average.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) : "<null>")})");
    Console.WriteLine();

    if (passed)
    {
        Console.WriteLine("METRICS READ-BACK PASSED — the recorded score is queryable through the Metrics API.");
        return 0;
    }

    Console.WriteLine("METRICS READ-BACK FAILED — see above. (Metrics ingestion can lag; increase the poll window.)");
    return 1;
}

// ── Prompt-linking mode: prove SetPrompt links a generation to a managed prompt ──
static async Task<int> RunPromptLinkingCheckAsync()
{
    var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
    var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");
    var host = Environment.GetEnvironmentVariable("LANGFUSE_HOST") ?? "http://localhost:3000";
    if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
    {
        Console.WriteLine("This check requires a LIVE Langfuse instance (set LANGFUSE_PUBLIC_KEY / LANGFUSE_SECRET_KEY).");
        return 2;
    }

    Console.WriteLine($"[setup] Langfuse host: {host}");

    const string promptName = "needlr-conformance-prompt";
    const int promptVersion = 1;
    var runId = $"needlr-prompt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

    using var http = new HttpClient { BaseAddress = new Uri(host.TrimEnd('/') + "/") };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}")));

    // Link-only assumes managed prompts already exist; ensure one (version 1) via the raw
    // prompt-management API so there is a prompt to link to.
    await EnsureTextPromptAsync(http, promptName);
    Console.WriteLine($"[setup] Ensured managed prompt '{promptName}'.");

    var options = new LangfuseOptions
    {
        PublicKey = publicKey,
        SecretKey = secretKey,
        Host = host,
        ServiceName = "needlr-langfuse-prompt-linking",
        Environment = "needlr-conformance",
        ScoreFailureMode = LangfuseScoreFailureMode.Strict,
        DiagnosticsCallback = msg => Console.WriteLine($"[langfuse] {msg}"),
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
        Console.WriteLine("[error] Langfuse export did not enable — check keys/host.");
        return 2;
    }

    string? traceId;
    using (var scenario = langfuse.BeginScenario("prompt-linking: summary", sessionId: runId, tags: ["conformance"]))
    {
        scenario.SetPrompt(promptName, promptVersion);
        traceId = scenario.TraceId;
        Console.WriteLine($"[run] trace {traceId}, linking prompt '{promptName}' v{promptVersion}.");
        await RunMockAsync(loop, diagnosticsAccessor, "prompt-linking");
    }

    langfuse.Flush(TimeSpan.FromSeconds(10));
    Console.WriteLine("[run] Flushed. Verifying the generation links to the prompt...");
    Console.WriteLine();

    if (string.IsNullOrEmpty(traceId))
    {
        Console.WriteLine("[error] No trace id was produced.");
        return 2;
    }

    var detail = string.Empty;
    var linked = await PollAsync("prompt-linked generation", 15, TimeSpan.FromSeconds(2), async () =>
    {
        var (ok, d) = await CheckGenerationPromptAsync(http, traceId, promptName, promptVersion);
        detail = d;
        return ok;
    });

    Console.WriteLine();
    Console.WriteLine($"  generation linked to prompt: {Mark(linked)}  {detail}");
    Console.WriteLine();

    if (linked)
    {
        Console.WriteLine("PROMPT LINKING PASSED — the generation references the managed prompt.");
        return 0;
    }

    Console.WriteLine("PROMPT LINKING FAILED — see above.");
    return 1;
}

static async Task EnsureTextPromptAsync(HttpClient http, string name)
{
    var payload = new { name, type = "text", prompt = "You are a concise summariser.", labels = new[] { "production" } };
    using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    using var response = await http.PostAsync("api/public/v2/prompts", content);
    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[setup] prompt ensure returned {(int)response.StatusCode}: {body}");
    }
}

static async Task<(bool Ok, string Detail)> CheckGenerationPromptAsync(HttpClient http, string traceId, string promptName, int promptVersion)
{
    var body = await GetRawAsync(http, $"api/public/observations?traceId={Uri.EscapeDataString(traceId)}&limit=50");
    if (body is null)
    {
        return (false, "(observations not found)");
    }

    using var json = JsonDocument.Parse(body);
    if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
    {
        return (false, "(no data)");
    }

    foreach (var obs in data.EnumerateArray())
    {
        var type = obs.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type != "GENERATION")
        {
            continue;
        }

        var name = obs.TryGetProperty("promptName", out var pn) && pn.ValueKind == JsonValueKind.String ? pn.GetString() : null;
        var version = obs.TryGetProperty("promptVersion", out var pv) && pv.ValueKind == JsonValueKind.Number ? pv.GetInt32() : (int?)null;

        if (string.Equals(name, promptName, StringComparison.Ordinal) && version == promptVersion)
        {
            return (true, $"(promptName={name}, promptVersion={version})");
        }

        if (name is not null)
        {
            return (false, $"(generation promptName={name}, promptVersion={version})");
        }
    }

    return (false, "(no linked GENERATION observation yet)");
}

static async Task<IAgentRunDiagnostics?> RunMockAsync(
    IIterativeAgentLoop loop,
    IAgentDiagnosticsAccessor accessor,
    string loopName,
    string instructions = "You summarise cached prompt content.",
    CancellationToken cancellationToken = default)
{
    var loopOptions = new IterativeLoopOptions
    {
        Instructions = instructions,
        PromptFactory = _ => "Summarize the cached prompt content.",
        Tools = [],
        MaxIterations = 1,
        IsComplete = _ => true,
        LoopName = loopName,
    };

    using (accessor.BeginCapture())
    {
        await loop.RunAsync(
            loopOptions,
            new IterativeContext { Workspace = new InMemoryWorkspace() },
            cancellationToken);
        return accessor.LastRunDiagnostics;
    }
}

static async Task<string?> GetRawAsync(HttpClient http, string path)
{
    using var response = await http.GetAsync(path);
    return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
}

static async Task<(bool Ok, string Detail)> PollTraceContextAsync(HttpClient http, string traceId, string expectedEnvironment, string expectedRelease)
{
    for (var i = 1; i <= 15; i++)
    {
        var body = await GetRawAsync(http, $"api/public/traces/{Uri.EscapeDataString(traceId)}");
        if (body is not null)
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var env = GetString(root, "environment");
            var release = GetString(root, "release");
            var isPublic = root.TryGetProperty("public", out var p) && p.ValueKind == JsonValueKind.True;
            var hasInput = root.TryGetProperty("input", out var inp) && inp.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
            var hasOutput = root.TryGetProperty("output", out var outp) && outp.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

            if (string.Equals(env, expectedEnvironment, StringComparison.Ordinal)
                && string.Equals(release, expectedRelease, StringComparison.Ordinal)
                && isPublic && hasInput && hasOutput)
            {
                return (true, $"(env={env}, release={release}, public={isPublic}, input+output present)");
            }

            if (i == 15)
            {
                return (false, $"(env={env}, release={release}, public={isPublic}, input={hasInput}, output={hasOutput})");
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    return (false, "(trace not found)");
}

static string? GetString(JsonElement element, string property) =>
    element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;

static async Task<HashSet<string>> GetScoreNamesByQueryAsync(HttpClient http, string query)
{
    var names = new HashSet<string>(StringComparer.Ordinal);

    using var response = await http.GetAsync($"api/public/v3/scores?{query}&limit=100");
    if (!response.IsSuccessStatusCode)
    {
        return names;
    }

    using var stream = await response.Content.ReadAsStreamAsync();
    using var json = await JsonDocument.ParseAsync(stream);
    if (json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
    {
        foreach (var score in data.EnumerateArray())
        {
            if (score.TryGetProperty("name", out var name) && name.GetString() is { } n)
            {
                names.Add(n);
            }
        }
    }

    return names;
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
    private readonly string _modelId;
    private readonly ChatClientMetadata _metadata;

    public MockChatClient(string modelId = "mock-model")
    {
        _modelId = modelId;
        _metadata = new ChatClientMetadata(
            providerName: "mock-provider",
            providerUri: new Uri("https://api.example.com:443"),
            defaultModelId: modelId);
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, "Mock summary of the cached prompt content.")])
        {
            ModelId = _modelId,
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
