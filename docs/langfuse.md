# Langfuse

Send Needlr agent telemetry and `Microsoft.Extensions.AI.Evaluation` scores to
[Langfuse](https://langfuse.com) so agent runs and evals show up on the Langfuse
dashboard — with a single call.

Needlr already emits OpenTelemetry traces and metrics using the GenAI semantic
conventions (`gen_ai.*`), which Langfuse understands natively. The
`NexusLabs.Needlr.AgentFramework.Langfuse` package adds the missing piece: an OTLP
exporter pointed at Langfuse, per-scenario trace grouping, and a bridge that turns
evaluator metrics into Langfuse scores.

## Quick Start

Install the package and start a session from environment variables:

```bash
export LANGFUSE_PUBLIC_KEY="pk-lf-..."
export LANGFUSE_SECRET_KEY="sk-lf-..."
# Optional — defaults to Langfuse Cloud (EU). Set for self-hosted or other regions:
# export LANGFUSE_HOST="http://localhost:3000"
```

```csharp
using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Langfuse;

// 1. Start export (no-ops cleanly when credentials are absent).
using var langfuse = LangfuseTelemetry.Start(LangfuseOptions.FromEnvironment());

// 2. One Langfuse trace per eval scenario.
using (var scenario = langfuse.BeginScenario(
    name: "trip-planner: NYC -> Tokyo",
    sessionId: runId,
    tags: ["regression"]))
{
    // Agent telemetry produced here nests under the scenario trace.
    var run = await runner.RunAsync(config, hooks, cancellationToken);

    var inputs = run.Diagnostics!.ToEvaluationInputs();
    var result = await new EfficiencyEvaluator(tokenBudget: 200_000).EvaluateAsync(
        inputs.Messages, inputs.ModelResponse, additionalContext: [new AgentRunDiagnosticsContext(run.Diagnostics!)]);

    // 3. Evaluator metrics become Langfuse scores on this trace.
    await result.RecordLangfuseScoresAsync(scenario);
}

langfuse.Flush();
```

That is the entire integration. When `LANGFUSE_PUBLIC_KEY` / `LANGFUSE_SECRET_KEY`
are not set (or no `Host`/`Region` target is chosen), the session is disabled and
every call becomes a no-op, so the same code runs unchanged in credential-less CI.

### Even shorter: evaluate and record in one call

`EvaluateAndRecordAsync` runs a set of evaluators and records every resulting metric
as a score, collapsing the per-test boilerplate:

```csharp
using (var scenario = langfuse.BeginScenario("trip-planner", sessionId: runId))
{
    var run = await runner.RunAsync(config, hooks, cancellationToken);
    var inputs = run.Diagnostics!.ToEvaluationInputs();

    await scenario.EvaluateAndRecordAsync(
        evaluators: [new EfficiencyEvaluator(tokenBudget: 200_000), new IterationCoherenceEvaluator(maxIterations: 20)],
        messages: inputs.Messages,
        modelResponse: inputs.ModelResponse,
        additionalContext: [new AgentRunDiagnosticsContext(run.Diagnostics!)]);
}
```

Two runnable examples live under `src/Examples/AgentFramework/`:

- `LangfuseEvaluationApp` — the full flow with no LLM or Langfuse credentials required (no-ops cleanly without keys).
- `LangfuseConformanceApp` — a small Langfuse-supported eval that **reads the trace and scores back** from a live Langfuse (local Docker by default) to prove ingestion. Not part of CI; run it by hand after standing Langfuse up. Run it with the `resiliency` argument to instead prove **graceful degradation when Langfuse is unreachable** (no server required): the eval still passes and every dropped score is surfaced.

## What appears in Langfuse

| Needlr telemetry | Langfuse |
|---|---|
| `agent.chat` / `agent.chat.stream` spans | Generations with model + token usage |
| `agent.tool {name}` spans | Tool-call observations |
| `gen_ai.usage.*` tags | Token usage / cost on each generation |
| Scenario root span | The trace (named, with session id, tags, metadata) |
| Evaluator metrics (via `RecordLangfuseScoresAsync`) | Scores attached to the trace |

## Scores

Langfuse does **not** ingest scores through OpenTelemetry span attributes — scores
are sent to the Langfuse Scores API. `RecordLangfuseScoresAsync` (and the
`ILangfuseScenario.RecordEvaluationAsync` / `RecordScoreAsync` methods) handle this
for you, keyed by the scenario's trace id. The mapping is:

| MEAI metric | Langfuse score type |
|---|---|
| `NumericMetric` | `NUMERIC` |
| `BooleanMetric` | `BOOLEAN` (`1`/`0`) |
| `StringMetric` | `CATEGORICAL` |

Each metric's `Reason` is sent as the score comment. Metrics whose value is unset
are skipped. Scores can be recorded while the scenario is still open — Langfuse
links them to the trace once it arrives.

Score names are sent verbatim by default (preserving the evaluator's authored metric
name). For cleaner dashboard filtering and grouping, enable
`NormalizeScoreNames` to send `snake_case` names (e.g. `all_tool_calls_succeeded`) —
this is the recommended shape unless you specifically want the authored names.

### Score-upload failures are non-fatal by default

A score is a dashboard write that happens *after* an eval has already produced its
verdict, so a transient Langfuse outage must not turn a green eval red. By default
(`ScoreFailureMode.NonFatal`) a failed upload increments `ILangfuseSession.ScoresFailed`
and invokes `ScoreErrorCallback` (wire it to your logger) but does **not** throw. Set
`ScoreFailureMode.Strict` if a missing score should hard-fail the caller.

```csharp
var options = LangfuseOptions.FromEnvironment();
options.ScoreErrorCallback = e => logger.LogWarning(e.Exception, "Langfuse score {Name} not recorded", e.ScoreName);
```

### Span enrichment

When exporting, a span processor sets two things Langfuse cares about so they don't
depend on implicit inference:

- `langfuse.observation.type` = `generation` on `agent.chat` / `agent.chat.stream`
  spans and `span` on `agent.tool` spans.
- `langfuse.observation.usage_details` (JSON) projected from Needlr's `gen_ai.usage.*`
  tags, so `input`, `output`, `cache_read_input_tokens`, and `reasoning_tokens` land
  reliably. When a span *also* carries MEAI's `gen_ai.usage.*` attributes, the explicit
  `langfuse.observation.usage_details` **cleanly replaces** them — Langfuse does not sum
  the two, so there is no double-counting. (Langfuse derives **cost** from a model-price
  table; register prices for your model names if you want cost populated. Note that
  provider/SDK model names such as Copilot's will not match Langfuse's built-in table,
  so `costDetails` stays empty until you add a custom model definition.)

!!! note "Trace-level filtering is by trace, not by observation"
    Trace-level attributes (`name`, `tags`, `metadata`) are set on the scenario root span,
    which is what Langfuse uses to build the trace. `session.id` and `user.id` are also
    propagated to child spans (via baggage) so you can filter observations by them. Filtering
    individual *observations* by `tags`/`metadata` is not supported — those live at the trace
    level, which matches the per-scenario grouping model.

## Experiments (datasets and runs)

Map each eval case to a Langfuse **dataset item**, then on every run link the trace it
produces as a **dataset run item**. Langfuse's experiment-comparison view lines runs up
side by side and shows how scores move across commits.

```csharp
// Once, at suite startup: ensure the dataset and its items exist.
await langfuse.Datasets.EnsureDatasetAsync("trip-planner-evals");
await langfuse.Datasets.UpsertItemAsync(new LangfuseDatasetItem
{
    DatasetName = "trip-planner-evals",
    Id = "nyc-tokyo",                 // stable id → re-running upserts, never duplicates
    Input = new { from = "NYC", to = "Tokyo" },
    ExpectedOutput = "a 3-stop itinerary",
});

// Per run: name it after something comparable (a git SHA, a CI run id).
var run = langfuse.BeginExperimentRun("trip-planner-evals", runName: gitSha);

foreach (var item in items)
{
    using var scenario = await run.BeginItemAsync(item.Id);
    var result = await RunAndEvaluate(item);       // your agent + evaluators
    await scenario.RecordEvaluationAsync(result);  // scores roll up into the run
}
```

The dataset and its items must exist before the run links to them. Run-item link failures
are non-fatal (surfaced via `DiagnosticsCallback`) so a Langfuse hiccup never crashes the
eval.

## Score configs

By default, scores are sent untyped. Declaring a **score config** once gives a score a
defined data type, numeric range, or category set, so the dashboard renders consistent
ranges and colors and validates incoming values. Match the config name to the score name
you record.

```csharp
await langfuse.ScoreConfigs.EnsureScoreConfigAsync(new LangfuseScoreConfig
{
    Name = "correctness",
    DataType = LangfuseScoreDataType.Numeric,
    MinValue = 0,
    MaxValue = 1,
});

await langfuse.ScoreConfigs.EnsureScoreConfigAsync(new LangfuseScoreConfig
{
    Name = "verdict",
    DataType = LangfuseScoreDataType.Categorical,
    Categories = [new("pass", 1), new("fail", 0)],
});
```

`EnsureScoreConfigAsync` is idempotent — it creates the config only when one of that name
does not already exist — so it is safe to call on every run.

## Observation- and session-level scores

Beyond whole-trace scores, you can score a single observation (a specific generation or
tool call) or a whole session (a multi-turn conversation spanning traces):

```csharp
// Score one observation within a trace (host path: you hold the ids).
await scoreClient.RecordObservationScoreAsync(traceId, observationId, "tool_correct", true);

// Score a whole session.
await scoreClient.RecordSessionScoreAsync(sessionId, "resolved", 0.8);

// From an eval scenario that was started with a sessionId:
await scenario.RecordSessionScoreAsync("resolved", true);
```

## Metrics read-back (CI quality gates)

Read aggregates back from Langfuse via the Metrics API — for example to fail a CI run if the
average eval score regressed. `session.Metrics` (or a DI-injected `ILangfuseMetricsClient`)
exposes a typed query plus a convenience for the common case:

```csharp
// after recording scores and flushing (allow a few seconds for ingestion):
var avg = await langfuse.Metrics.GetScoreAverageAsync(
    "correctness", fromTimestamp: runStart, toTimestamp: DateTimeOffset.UtcNow, environment: "ci");

if (avg is < 0.8)
    throw new InvalidOperationException($"correctness regressed: {avg}");
```

For arbitrary aggregates, use `QueryAsync` with a `LangfuseMetricsQuery` (views `observations`,
`scores-numeric`, `scores-categorical`; measures, dimensions, filters). Result columns are named
`{aggregation}_{measure}` (e.g. `avg_value`), and `result.GetScalar("avg", "value")` reads the
single aggregate row of an ungrouped query.

!!! note "Ingestion is asynchronous"
    Flush and allow a few seconds for ingestion before querying (this loop ingested in ~7s on a
    local instance). Metrics API availability can vary by deployment version — verify against your
    target instance.

## Prompt linking

Link the generations in a scenario to a versioned prompt managed in Langfuse, so you can
analyze scores **by prompt version** (prompt-regression tracking). Call `SetPrompt` before
running the agent — Needlr stamps `langfuse.observation.prompt.name` / `version` on the
chat-completion (generation) spans; tool spans are unaffected:

```csharp
using var scenario = langfuse.BeginScenario("trip-planner");
scenario.SetPrompt("trip-planner", version: 7);   // link generations to a managed prompt + version
var result = await RunAndEvaluate(...);
```

The prompt must already exist in Langfuse prompt management for the link to resolve. Pass
`version: null` to link by name only.

## Trace context: environment, release, and more

Set a deployment **environment** (e.g. `ci`, `staging`, `production`) and a **release**
(e.g. a git SHA) once — they are propagated to every exported span, so Langfuse keeps CI
eval noise out of production dashboards and lets you compare metrics across releases:

```csharp
var options = LangfuseOptions.FromEnvironment();
options.Environment = "ci";
options.Release = gitSha;
```

Per scenario you can also enrich the trace:

```csharp
scenario.SetInput(item.Input);       // trace-level input shown at the top of the trace
scenario.SetOutput(finalAnswer);     // trace-level output (e.g. the agent's final answer)
scenario.SetVersion("prompt-v7");    // langfuse.version
scenario.SetTracePublic();           // shareable URL — handy for linking a failure in a PR
```

## Comments

Attach context to a trace — a CI run URL, a git commit, the failing assertion message.
**Comments are a post-flush operation:** unlike scores (which Langfuse can link to a trace
that arrives later), Langfuse rejects a comment whose target trace does not yet exist. So
add comments *after* the trace has been flushed and ingested, keyed by trace id:

```csharp
// after langfuse.Flush() and the trace has been ingested:
await langfuse.AddTraceCommentAsync(traceId, $"CI run {ciUrl} — expected 3 stops, got 2");
```

Comments are non-fatal; a failure is reported through `DiagnosticsCallback` rather than
thrown.

## Composing with MEAI OpenTelemetry

Needlr's diagnostics middleware and MEAI's `OpenTelemetryChatClient` /
`UseOpenTelemetry()` can both create spans for the same chat call. To get the
richest `gen_ai` spans without duplicates, enable MEAI's OpenTelemetry and set
Needlr's chat-completion activity mode to enrich the parent span:

```csharp
.UsingAgentFramework(af => af
    .ConfigureMetrics(o => o.ChatCompletionActivityMode = ChatCompletionActivityMode.EnrichParent)
    .UsingDiagnostics())
```

See [GenAI Token Metrics](gen-ai-token-metrics.md) for how Needlr and MEAI share
the `gen_ai.client.token.usage` histogram.

## Configuration

`LangfuseOptions.FromEnvironment()` reads `LANGFUSE_PUBLIC_KEY`,
`LANGFUSE_SECRET_KEY`, and `LANGFUSE_HOST`. All values can also be set in code.

| Option | Default | Description |
|---|---|---|
| `PublicKey` / `SecretKey` | _(from env)_ | Langfuse API keys. Both required to export. |
| `Host` | _(unset)_ | Base URL (e.g. `http://localhost:3000`). One of `Host` or `Region` is **required**. |
| `Region` | _(unset)_ | Langfuse Cloud region: `Eu`, `Us`, `Jp`, `Hipaa`. Setting it is an explicit opt-in to cloud export. |
| `Enabled` | `true` | Set `false` to force a no-op even with credentials. |
| `ServiceName` | `needlr-agent` | OpenTelemetry `service.name` resource attribute. |
| `Environment` | _(unset)_ | Deployment environment (e.g. `ci`, `production`), emitted as `langfuse.environment` on every span so Langfuse partitions the data. |
| `Release` | _(unset)_ | Release identifier (e.g. a git SHA), emitted as `langfuse.release` for cross-release comparison. |
| `IncludeMetrics` | `false` | Export Needlr's `gen_ai` metrics. Off by default — see note below. |
| `ScoreFailureMode` | `NonFatal` | `NonFatal` records a failed score upload (counter + callback) without throwing; `Strict` throws. |
| `ScoreErrorCallback` | _(none)_ | Invoked with a `LangfuseScoreError` when a score upload fails under `NonFatal`. |
| `NormalizeScoreNames` | `false` | When `true`, score names are normalised to `snake_case` for consistent dashboard filtering. |
| `DiagnosticsCallback` | _(none)_ | Receives library diagnostic messages (e.g. the "no export target" warning). Wire to your logger. |
| `SamplingRatio` | `1.0` | Head-based trace sampling ratio (eval workloads want `1.0`). |
| `AgentActivitySourceName` | `NexusLabs.Needlr.AgentFramework` | Needlr agent span source to export. |
| `GenAiMeterName` | `Experimental.Microsoft.Extensions.AI` | Meter owning `gen_ai.client.token.usage`. |
| `AdditionalActivitySources` / `AdditionalMeters` | _(empty)_ | Extra sources/meters to export. |

!!! warning "Cloud export is opt-in (no silent egress)"
    Providing only API keys is **not** enough to export — you must also set an explicit target
    (`Host` for self-hosted, or `Region` for Langfuse Cloud). This prevents accidentally sending
    traces (which may include prompts, agent outputs, and customer data) to Langfuse Cloud. When
    keys are present but no target is set, export is disabled and `DiagnosticsCallback` receives a
    one-line explanation.

!!! warning "OTLP metrics are not ingested by Langfuse (as of v3.x)"
    Langfuse's OTLP metrics endpoint accepts requests (returns HTTP 200) but does **not**
    ingest the data, and there is no metrics read API — exported metrics are silently
    discarded. `IncludeMetrics` is therefore `false` by default. Token usage already rides
    on the generation spans (see below), so you lose nothing. Enable it only when pointing
    the exporter at a backend that ingests OTLP metrics.

If you customised Needlr's telemetry source names via `ConfigureMetrics(...)`, set
the matching `AgentActivitySourceName` / `AgentMeterName` / `GenAiMeterName` so those
streams are exported.

### ASP.NET Core and generic hosts

For applications that already call `AddOpenTelemetry()`, register Langfuse export on
the host pipeline instead of starting a standalone session:

```csharp
builder.Services.AddNeedlrLangfuse(options =>
{
    options.ServiceName = "my-agent-service";
});
```

This wires the OTLP exporter into the host's tracer and meter providers so they
share the application lifecycle, and registers an `ILangfuseScoreClient` for scoring
request traces by id:

```csharp
public sealed class MyHandler(ILangfuseScoreClient scores)
{
    public async Task HandleAsync(/* ... */)
    {
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
        if (traceId is not null)
        {
            await scores.RecordScoreAsync(traceId, "helpfulness", value: true);
        }
    }
}
```

When Langfuse is not configured, a disabled no-op `ILangfuseScoreClient` is registered,
so injection always succeeds and host code never needs to branch on configuration.

`AddNeedlrLangfuse` also registers `ILangfuseDatasetClient` and `ILangfuseScoreConfigClient`
(both disabled no-ops when unconfigured) for managing datasets and score configs from a host
application.

## Langfuse Cloud vs self-hosted

- **Cloud**: leave `Host` unset and set `Region` explicitly (`Eu`, `Us`, `Jp`, or `Hipaa`).
- **Self-hosted**: set `Host` to your deployment, e.g. `http://localhost:3000`.
  The OpenTelemetry endpoint requires Langfuse `v3.22.0` or newer.

One of `Host` or `Region` must be set — there is no default cloud target, so traces
are never sent to Langfuse Cloud unless you opt in.

Langfuse ingests OTLP over **HTTP** (`HTTP/protobuf`); gRPC is not supported, so the
exporter is always configured for HTTP.
