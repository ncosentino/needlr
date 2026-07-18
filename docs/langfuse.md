# Langfuse

Send Needlr agent telemetry and `Microsoft.Extensions.AI.Evaluation` scores to
[Langfuse](https://langfuse.com) so agent runs and evals show up on the Langfuse
dashboard — with a single call.

Needlr already emits OpenTelemetry traces and metrics using the GenAI semantic
conventions (`gen_ai.*`), which Langfuse understands natively. The
`NexusLabs.Needlr.AgentFramework.Langfuse` package adds the missing piece: an OTLP
exporter pointed at Langfuse, per-scenario trace grouping, and a bridge that turns
evaluator metrics into Langfuse scores. It supports both exporter-owning standalone
sessions and a complete non-owning facade for dependency-injection-based hosts.

## Quick Start

Install the package and start a session from environment variables:

```bash
export LANGFUSE_PUBLIC_KEY="pk-lf-..."
export LANGFUSE_SECRET_KEY="sk-lf-..."
# Required unless Region is configured in code:
export LANGFUSE_HOST="http://localhost:3000"
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
    await result.RecordLangfuseScoresAsync(
        scenario,
        new LangfuseEvaluationScoreOptions
        {
            ScoreIdProvider = metric => $"{runId}:{metric.Name}",
        },
        cancellationToken);
}

var shutdown = langfuse.Shutdown(TimeSpan.FromSeconds(5));
var publication = langfuse.PublicationHealth.GetSnapshot();
Console.WriteLine($"traces={shutdown.Traces}, metrics={shutdown.Metrics}");
Console.WriteLine(
    $"trace dropped={publication.TraceExport.Dropped}, " +
    $"score failed={publication.ScoreUploads.Failed}, " +
    $"retries={publication.Retries.Total}, drain={publication.Drain.Status}");
```

That is the entire integration. When `LANGFUSE_PUBLIC_KEY` / `LANGFUSE_SECRET_KEY`
are not set (or no `Host`/`Region` target is chosen), the session is disabled and
every call becomes a no-op, so the same code runs unchanged in credential-less CI.

### Flush versus final shutdown

`Flush(timeout)` asks the active session to export telemetry buffered before the call and
keeps the session open. Use an explicit timeout when flushing before read-back operations.

`Shutdown(timeout)` is final: it shares one timeout budget across trace and metric providers,
releases every resource owned by the standalone session, and rejects new session operations.
It returns a `LangfuseShutdownOutcome` with separate trace and metric statuses. Those statuses
describe the local OpenTelemetry drain only—they do **not** guarantee durable Langfuse ingestion.

`Dispose()` performs the same final shutdown with `LangfuseOptions.ShutdownTimeout`, which
defaults to five seconds. Set `Timeout.InfiniteTimeSpan` only when an unbounded process-exit wait
is explicitly required.

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
        options: new LangfuseEvaluateAndRecordOptions(
            chatConfiguration: null,
            additionalContext: [new AgentRunDiagnosticsContext(run.Diagnostics!)],
            scoreOptions: new LangfuseEvaluationScoreOptions
            {
                ScoreIdProvider = metric => $"{runId}:{metric.Name}",
            }),
        cancellationToken: cancellationToken);
}
```

Use the four-argument overload when chat configuration, additional context, score identity, and
cancellation all use their defaults. The explicit overload requires one
`LangfuseEvaluateAndRecordOptions` value plus an explicit cancellation token. The options constructor
snapshots additional context, and a `null` context sequence becomes an empty read-only collection.

Two runnable examples live under `src/Examples/AgentFramework/`:

- `LangfuseEvaluationApp` — the full flow with no LLM or Langfuse credentials required (no-ops cleanly without keys).
- `LangfuseConformanceApp` — a small Langfuse-supported eval that **reads the trace and scores back** from a live Langfuse (local Docker by default) to prove ingestion. It also has no-server modes:
  - `resiliency` proves cancellation, context-safe experiment item callbacks, safe trace propagation, structured link failure, score-failure reporting, and bounded shutdown while Langfuse is unreachable.
  - `dependency-injection` proves `ILangfuseClient` exposes the complete evaluation surface through one host-owned telemetry pipeline without registering a standalone session.

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

Each score and evaluation API exposes two overloads: one with only the required
arguments, and one with nullable options plus an explicit `CancellationToken`.
To customize either concern, call the full overload and pass `null` or
`CancellationToken.None` for the concern you are not customizing. There are no
token-only or options-only overloads.

### Stable score IDs and safe retries

Supply `LangfuseScoreOptions.Id` when a score publication must be safely repeatable:

```csharp
await scenario.RecordScoreAsync(
    "correctness",
    0.94,
    new LangfuseScoreOptions
    {
        Id = $"{runId}:case-42:correctness",
        Comment = "All required facts were present.",
    },
    cancellationToken);
```

For a complete `EvaluationResult`, provide one stable id per metric:

```csharp
await scenario.RecordEvaluationAsync(
    result,
    new LangfuseEvaluationScoreOptions
    {
        ScoreIdProvider = metric => $"{runId}:case-42:{metric.Name}",
    },
    cancellationToken);
```

Langfuse uses the request-body `id` together with the score name and its server-assigned UTC date.
There is no `Idempotency-Key` header. Needlr automatically retries score requests only when `Id`
is present, always resending the complete unchanged payload. A retry that crosses a Langfuse UTC
date boundary can still create another score, so callers should treat a stable id as bounded to
one publication window rather than as a permanent global upsert key.

GET requests and identical dataset upserts are also retry-safe. Other POST operations—including
experiment item links, comments, prompts, dataset items, score-config creation, and model
creation—are never retried blindly.

### Score-upload failures are non-fatal by default

A score is a dashboard write that happens *after* an eval has already produced its
verdict, so a transient Langfuse outage must not turn a green eval red. By default
(`ScoreFailureMode.NonFatal`) a failed upload increments
`PublicationHealth.GetSnapshot().ScoreUploads.Failed` and invokes `ScoreErrorCallback`
(wire it to your logger) but does **not** throw. Set
`ScoreFailureMode.Strict` if a missing score should hard-fail the caller.

Caller-requested cancellation is never converted into a non-fatal score or publication
failure. An `OperationCanceledException` propagates so a cancelled evaluation stops promptly.

```csharp
var options = LangfuseOptions.FromEnvironment();
options.ScoreErrorCallback = e => logger.LogWarning(e.Exception, "Langfuse score {Name} not recorded", e.ScoreName);
```

### Publication health

`ILangfuseClient.PublicationHealth` reports trace export, direct REST publication, retries, and
local drain separately:

```csharp
LangfusePublicationHealthSnapshot health =
    langfuse.PublicationHealth.GetSnapshot();

Console.WriteLine(health.TraceExport.LocallyEnqueued);
Console.WriteLine(health.TraceExport.Dropped);
Console.WriteLine(health.TraceExport.Acknowledged);
Console.WriteLine(health.TraceExport.Failed);
Console.WriteLine(health.ScoreUploads.Failed);
Console.WriteLine(health.ItemLinks.Failed);
Console.WriteLine(health.Retries.RateLimited);
Console.WriteLine(health.Drain.Status);
Console.WriteLine(health.Drain.Duration);
```

The stages are intentionally distinct:

- `LocallyObserved` means the custom processor saw a recorded activity.
- `LocallyEnqueued` means the bounded local queue accepted it.
- `Dropped` means the local queue rejected it because it was full or closing.
- `Acknowledged` means the OTLP exporter returned success for the batch.
- `Drain.Completed` means the local provider removed the requested work before its timeout.

None of those values proves that Langfuse durably processed the event or made it queryable.
Use read-back through Langfuse's public API when a workflow requires end-to-end confirmation.
REST `Succeeded` counts likewise mean that Langfuse accepted the API request, not that asynchronous
materialization completed.

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

!!! note "Trace context is propagated through an allowlist"
    Needlr propagates the scenario name, tags, metadata, version, session id, and user id to
    in-process child spans so Langfuse can filter and aggregate observations reliably. Scenario
    context is not stored in W3C baggage, and arbitrary inherited OpenTelemetry baggage is **not**
    copied into exported span attributes. This prevents unrelated, sensitive, or high-cardinality
    application context from being sent to Langfuse or downstream services accidentally.

## Experiments (datasets and runs)

The provider-neutral runner can materialize hosted Langfuse datasets through
`LangfuseDatasetCaseSource<TCase>`. Langfuse owns remote dataset/version semantics while the core
runner continues to own scheduling, retries, evaluation, and quality policy.

### Hosted dataset case source

`LangfuseDatasetSelection.Version` is an optional UTC timestamp. Omit it for the latest dataset state
at query time, or set it to reproduce the item state at an explicit Langfuse dataset version:

```csharp
using System.Text.Json;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;
using NexusLabs.Needlr.AgentFramework.Langfuse;

public sealed record TripPlannerCase(
    JsonElement? Input,
    JsonElement? ExpectedOutput,
    JsonElement? Metadata,
    string? SourceTraceId,
    string? SourceObservationId);

var selection = new LangfuseDatasetSelection
{
    Name = "trip-planner-evals",
    Version = DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
};

var source = new LangfuseDatasetCaseSource<TripPlannerCase>(
    langfuse.Datasets,
    selection,
    item => new ExperimentCase<TripPlannerCase>
    {
        Id = item.Id,
        Value = new TripPlannerCase(
            item.Input,
            item.ExpectedOutput,
            item.Metadata,
            item.SourceTraceId,
            item.SourceObservationId),
        TrialCount = 3,
        Tags = ["hosted"],
    });

var langfuseRun = langfuse.BeginExperimentRun(
    selection.Name,
    runId,
    new LangfuseExperimentRunOptions
    {
        Description = "Trip planner regression suite",
        DatasetVersion = selection.Version,
    });
var langfuseScope =
    langfuse.CreateExperimentItemScopeProvider<TripPlannerCase, TripPlannerOutput>(
        langfuseRun,
        new LangfuseExperimentItemScopeOptions<TripPlannerCase>
        {
            ScenarioNameFactory = _ => "evaluate-trip-planner-item",
            FailureMode = ExperimentItemScopeFailureMode.BestEffort,
        });
var langfuseSink =
    langfuse.CreateExperimentResultSink<TripPlannerCase, TripPlannerOutput>(
        langfuseRun,
        new LangfuseExperimentResultSinkOptions<TripPlannerCase, TripPlannerOutput>
        {
            ItemScoreIdProvider = (item, metric) =>
                $"{runId}:{item.Case.Id}:{item.TrialIndex}:{metric.Name}",
            RunEvaluationScoreIdProvider = (evaluation, metric) =>
                $"{runId}:{evaluation.Name}:{metric.Name}",
            DecisionScore = new LangfuseExperimentDecisionScoreOptions
            {
                Name = "experiment_decision",
                ScoreIdProvider = _ => $"{runId}:decision",
            },
        });

var definition = new ExperimentDefinition<TripPlannerCase, TripPlannerOutput>
{
    Name = "trip-planner-regression",
    CaseSource = source,
    ItemScopes = [langfuseScope],
    Sinks = [langfuseSink],
    Task = RunTripPlannerAsync,
};
```

The source retrieves dataset metadata, follows every dataset-item page in provider order, excludes
archived items, and validates the complete selection before the runner starts any trial. Duplicate
item ids, changing or malformed pagination, inconsistent dataset identity, mapper duplicates, and
empty hosted datasets fail the load. Disabled Langfuse also fails explicitly instead of producing an
empty run.

The mapper must preserve `item.Id` as `ExperimentCase<TCase>.Id`. That provider-neutral case identity
is the binding used by the Langfuse per-trial scope to create a dataset-run-item link. Input, expected
output, metadata, source trace id, and source observation id remain available to the caller-owned
case value.

The resulting `ExperimentSourceReference` contains the hosted dataset name and id. Explicit version
timestamps are normalized to UTC; latest selections leave `Version` unset because the public
Langfuse API does not return a resolved version identifier.

For lower-level reads, `ILangfuseDatasetClient` also exposes validated `ListDatasetsAsync`,
`ListDatasetItemsAsync`, and fully materialized `GetDatasetAsync` operations.

### Per-trial trace and hosted link scope

`CreateExperimentItemScopeProvider<TCase,TOutput>` binds the generic runner to an existing
`ILangfuseExperimentRun`. One scenario trace is created when a statistical trial enters its scope.
Every operational retry and the item evaluator reactivate that same trace, while the hosted
dataset-run-item link is attempted at most once. The same run instance retains authoritative
dataset-run identity and is reused by the result sink in
[#53](https://github.com/ncosentino/needlr/issues/53).

When a task or item evaluator needs direct scenario APIs, resolve the exact feature:

```csharp
var scenario = context.Features.GetRequired<ILangfuseScenario>();
if (context.Case.Value.Input is { } input)
{
    scenario.SetInput(input);
}
```

The item result uses the `langfuse` correlation namespace with:

- `trace.id`;
- `dataset.run.item.id`;
- `dataset.run.id`.

`Linked` produces successful item publication. `Failed` and `Inconsistent` produce structured
publication failure without changing item quality in best-effort mode. `NotSampled` and `Disabled`
are `NotAttempted`. A required publication affects aggregate publication health, while
`ExecutionPrerequisite` independently selects strict link behavior and prevents the first attempt
when linking fails. Unsampled and disabled scopes never block execution.

The Langfuse scope returns only an `ExperimentItemPublicationOperationResult` containing status,
correlations, and an optional failure. The generic runner stamps the registered provider name and
requirement flag into the canonical `ExperimentItemPublicationResult`.

An explicit `LangfuseExperimentRunOptions.DatasetVersion` is normalized to UTC and submitted with
every item link. Use the same value as `LangfuseDatasetSelection.Version` so the comparison view
references the item state that the source loaded.

Hosted and local scope factories each expose two shapes only: a default overload without options and
an explicit overload that requires a non-null `LangfuseExperimentItemScopeOptions<TCase>`.

For local cases that are not hosted in Langfuse, use a trace-only scope:

```csharp
var localLangfuseScope =
    langfuse.CreateLocalExperimentItemScopeProvider<MyCase, MyOutput>();
```

Local recorded traces produce only `trace.id`; no fake dataset or dataset run is created. Scope
success means the trace lifecycle was created locally and does not claim durable OpenTelemetry
ingestion.

### Canonical result publication

`CreateExperimentResultSink<TCase,TOutput>` projects each canonical item `EvaluationResult` to its
correlated trace, each successful run evaluator to the authoritative dataset run, and an optional
categorical `ExperimentRunDecision` score. The sink never reruns evaluators or policies.

Hosted and local result-sink factories likewise expose a default overload and an explicit overload
that requires non-null `LangfuseExperimentResultSinkOptions<TCase,TOutput>`.

Use contextual score-id callbacks when provider retry or rerun idempotency matters. Item callbacks
receive case/trial identity, and run callbacks receive the evaluator identity, so equal metric names
do not collide across trials or evaluators. Langfuse's score identity still includes the score name
and UTC date; see `LangfuseScoreOptions` for the provider constraint.

The sink maps direct score outcomes independently from quality:

- accepted and intentionally skipped score projections complete successfully;
- nonfatal API failures return a structured failed sink result;
- strict score mode throws into the generic sink pipeline, which isolates later sinks;
- missing or inconsistent hosted dataset-run identity fails configured run-score publication;
- disabled and entirely unattempted publication remain `NotAttempted`;
- local mode publishes item trace scores and skips dataset-run/decision targets without fabricating
  a run.

The Langfuse sink returns only an `ExperimentSinkPublicationOperationResult`; the generic runner
stamps the configured sink name and requirement flag into the canonical `ExperimentSinkResult`.

`GetPublicationSnapshot()` retains ordered item and run-evaluation score results, the optional
decision score result, and the hosted run's existing link/run-score snapshot. REST acceptance does
not prove durable trace ingestion.

For local cases, pair the local scope with a local sink:

```csharp
var localScope =
    langfuse.CreateLocalExperimentItemScopeProvider<MyCase, MyOutput>();
var localSink =
    langfuse.CreateLocalExperimentResultSink<MyCase, MyOutput>();
```

The same definition shape works against the disabled facade. No credential branches are needed
inside the task, evaluator, policy, or runner.

Run the converged example without credentials, with local trace publication, or with a hosted
dataset:

```bash
dotnet run --project src/Examples/AgentFramework/LangfuseConformanceApp -- experiment-runner
dotnet run --project src/Examples/AgentFramework/LangfuseConformanceApp -- experiment-runner hosted my-dataset
```

The first command uses the coherent disabled facade when credentials are absent and automatically
uses local trace publication when credentials are present.

The API below is retained as the low-level/manual escape hatch for ad hoc item loops and direct run
scores. It is not the canonical bulk experiment workflow.

### Low-level manual publication

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
var run = langfuse.BeginExperimentRun(
    "trip-planner-evals",
    runName: gitSha,
    options: new LangfuseExperimentRunOptions
    {
        Description = "Trip planner regression suite",
        DatasetVersion = selection.Version,
        Metadata = new
        {
            candidate = new { model = "gpt-5", promptVersion = 7 },
            baseline = "main",
        },
    });

foreach (var item in items)
{
    var itemResult = await run.RunItemAsync(
        item.Id,
        async (scenario, cancellationToken) =>
        {
            var evaluation = await RunAndEvaluate(item, cancellationToken);
            await scenario.RecordEvaluationAsync(
                evaluation,
                new LangfuseEvaluationScoreOptions
                {
                    ScoreIdProvider = metric => $"{gitSha}:{item.Id}:{metric.Name}",
                },
                cancellationToken);
            return evaluation;
        },
        options: null,
        cancellationToken: cancellationToken);

    Console.WriteLine(
        $"{item.Id}: {itemResult.Link.Status}, run {itemResult.Link.DatasetRunId}");
}

if (run.DatasetRunId is { } datasetRunId)
{
    await run.RecordScoreAsync(
        "average_correctness",
        0.94,
        new LangfuseScoreOptions { Id = $"{gitSha}:average_correctness" },
        cancellationToken);
    await run.RecordScoreAsync(
        "passed",
        true,
        new LangfuseScoreOptions { Id = $"{gitSha}:passed" },
        cancellationToken);
    await run.RecordScoreAsync(
        "verdict",
        "acceptable",
        new LangfuseScoreOptions { Id = $"{gitSha}:verdict" },
        cancellationToken);
}

var publication = run.GetPublicationSnapshot();
Console.WriteLine(
    $"REST publication: {publication.ApiPublicationStatus}; " +
    $"trace ingestion verified: no");
```

`RunItemAsync` creates the scenario, links it, executes the callback while the scenario remains
the active `Activity.Current` across awaits, and disposes it before returning. Agent, chat, and
tool spans created inside the callback therefore nest beneath the item trace without callers
assigning `Activity.Current` or manually managing scenario lifetime.

The experiment-run methods use two explicit overloads: required arguments only, or required
arguments plus nullable options and an explicit cancellation token. To customize either concern,
call the full overload and pass `null` for default options or `CancellationToken.None` when
cancellation is not needed. There are no token-only or options-only overloads.

The dataset and its items must exist before the run links to them. Link failures use
`LangfuseExperimentItemLinkFailureMode.BestEffort` by default: the callback still runs,
`itemResult.Link.Status` is `Failed`, and details reach `DiagnosticsCallback`. Select strict behavior when
publication is a prerequisite:

```csharp
var itemResult = await run.RunItemAsync(
    item.Id,
    async (scenario, token) =>
    {
        var evaluation = await RunAndEvaluate(item, token);
        await scenario.RecordEvaluationAsync(
            evaluation,
            options: null,
            token);
        return evaluation;
    },
    new LangfuseExperimentItemOptions
    {
        LinkFailureMode = LangfuseExperimentItemLinkFailureMode.Strict,
    },
    cancellationToken);
```

Strict mode propagates `LangfuseException` and does not invoke the callback when an attempted link
fails. `NotSampled` and `Disabled` remain explicit statuses and still execute the callback.

Each successful item-link response supplies the authoritative Langfuse dataset-run id. The first
successful response resolves `run.DatasetRunId`; subsequent and parallel responses must agree.
Conflicting ids move `run.IdentityStatus` to `Inconsistent`, clear the aggregate `DatasetRunId`,
and make later run-level scores `NotAttempted` (or throw under strict score-failure mode). Every
item retains the id returned for its own remote link in `itemResult.Link.DatasetRunId`.

`LangfuseExperimentRunOptions.Metadata` is serialized once when the run is created and submitted
with every item link. This guarantees that whichever parallel request creates the remote run
receives the same metadata. `run.Metadata` exposes that frozen requested value; it does not claim
that an already-existing remote run was overwritten.

Run-level numeric, boolean, categorical, and MEAI evaluation scores target the resolved
`DatasetRunId`:

```csharp
await run.RecordScoreAsync(
    "average_accuracy",
    0.94,
    new LangfuseScoreOptions { Id = $"{runId}:average_accuracy" },
    cancellationToken);
await run.RecordScoreAsync(
    "passed",
    true,
    new LangfuseScoreOptions { Id = $"{runId}:passed" },
    cancellationToken);
await run.RecordScoreAsync(
    "verdict",
    "acceptable",
    new LangfuseScoreOptions { Id = $"{runId}:verdict" },
    cancellationToken);

IReadOnlyList<LangfuseExperimentRunScoreResult> results =
    await run.RecordEvaluationAsync(
        aggregateEvaluation,
        new LangfuseEvaluationScoreOptions
        {
            ScoreIdProvider = metric => $"{runId}:{metric.Name}",
        },
        cancellationToken);
```

The methods return `Accepted`, `Failed`, `NotAttempted`, `Skipped`, or `Disabled`. `Accepted`
means the Scores REST API accepted the request; it is not a durable read-back acknowledgement.

`GetPublicationSnapshot()` provides coherent item-link and run-score counts plus an aggregate
direct-API status. It deliberately does not claim that OpenTelemetry traces were ingested:

```csharp
LangfuseExperimentRunPublicationSnapshot snapshot = run.GetPublicationSnapshot();

Console.WriteLine(snapshot.IdentityStatus);
Console.WriteLine(snapshot.ItemLinks.Linked);
Console.WriteLine(snapshot.RunScores.Accepted);
Console.WriteLine(snapshot.ApiPublicationStatus);
```

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

`EnsureDatasetAsync` uses Langfuse's name-keyed dataset upsert directly. Identical concurrent calls
are safe to repeat after throttling, transport failure, or transient 5xx responses. Concurrent
calls that supply different descriptions are provider-defined last-writer-wins updates, so use one
canonical dataset definition.

Every `ILangfuseDatasetClient` member uses two explicit overloads: required arguments only (using
`CancellationToken.None` and, for `EnsureDatasetAsync`/`ListDatasetsAsync`/`ListDatasetItemsAsync`,
no description or page 1 of 50), or the full set of required arguments plus an explicit
`CancellationToken`. To customize paging or supply a description, call the full overload; there are
no token-only, description-only, or custom-page-without-a-token overloads.

Langfuse does not enforce score-config name uniqueness and does not accept a caller-supplied config
id. Needlr therefore acquires `LangfuseOptions.ResourceLockProvider`, re-lists every page inside
the lock, and compares the complete schema before creating. The default
`LangfuseInProcessResourceLockProvider` prevents duplicates within one process. Applications with
multiple workers or hosts must provide a distributed `ILangfuseResourceLockProvider` (for example,
backed by Redis or a database advisory lock). An existing config with the same name but a different
or ambiguous schema throws instead of silently accepting drift.

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

## Model pricing

Langfuse derives a generation's cost from a model-price table; models it does not recognise
(mock or internal model names) come back with no cost. Register a price so cost populates:

```csharp
await langfuse.Models.EnsureModelPriceAsync(new LangfuseModelPrice
{
    ModelName = "my-internal-model",
    MatchPattern = "(?i)^my-internal-model$",   // regex matched against the generation's model
    InputPrice = 0.000001,                       // USD per input token
    OutputPrice = 0.000002,                      // USD per output token
});
```

Langfuse model creation also lacks a reliable caller idempotency key and duplicate-name failures
can surface as different HTTP statuses. Needlr uses the same resource-lock contract, compares the
complete existing definition, and reconciles ambiguous transport/5xx failures by re-listing before
retrying. Use a distributed lock provider for multi-process initialization. Register prices
**before** the generations are ingested so Langfuse computes cost at ingestion time.

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

## Prompt management (fetching)

Fetch the prompt an eval runs against from Langfuse, so the prompt lives in Langfuse and the
generations link to the exact version used. `session.Prompts` (or a DI-injected
`ILangfusePromptClient`) fetches and creates managed prompts:

```csharp
var prompt = await langfuse.Prompts.GetPromptAsync("trip-planner", label: "production");

using var scenario = langfuse.BeginScenario("trip-planner");
scenario.SetPrompt(prompt!);                          // auto-links to prompt.Name + prompt.Version
var result = await RunAndEvaluate(prompt!.Text, ...); // use the fetched text as instructions
```

`GetPromptAsync(name, label?, version?)` returns `null` when the prompt is absent (or Langfuse is
unconfigured) — `version` takes precedence over `label`, and with neither the `production` label is
used. `CreateTextPromptAsync(name, text, labels?)` creates a new version. Passing the fetched
`LangfusePrompt` to `SetPrompt` is the link-only feature applied automatically.

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
| `ScoreFailureMode` | `NonFatal` | `NonFatal` records a failed score upload in publication health and invokes the callback without throwing; `Strict` throws. |
| `ScoreErrorCallback` | _(none)_ | Invoked with a `LangfuseScoreError` when a score upload fails under `NonFatal`. |
| `NormalizeScoreNames` | `false` | When `true`, score names are normalised to `snake_case` for consistent dashboard filtering. |
| `DiagnosticsCallback` | _(none)_ | Receives library diagnostic messages (e.g. the "no export target" warning). Wire to your logger. |
| `SamplingRatio` | `1.0` | Head-based trace sampling ratio (eval workloads want `1.0`). |
| `ShutdownTimeout` | `5 seconds` | Total trace + metric timeout budget used by standalone-session disposal. |
| `ResourceLockProvider` | in-process | Coordinates score-config and model creation. Use a distributed implementation when multiple processes initialize one project. |
| `AgentActivitySourceName` | `NexusLabs.Needlr.AgentFramework` | Needlr agent span source to export. |
| `GenAiMeterName` | `Experimental.Microsoft.Extensions.AI` | Meter owning `gen_ai.client.token.usage`. |
| `AdditionalActivitySources` / `AdditionalMeters` | _(empty)_ | Extra sources/meters to export. |

### REST timeout and retry controls

`LangfuseOptions.Http` exposes a bounded subset of transport policy:

| Option | Default | Description |
|---|---|---|
| `RequestTimeout` | `30 seconds` | Timeout for each individual REST attempt. |
| `MaxAttempts` | `3` | Total attempts for retry-safe operations; `1` disables retries. |
| `InitialRetryDelay` | `200 ms` | Initial exponential delay. |
| `MaxRetryDelay` | `5 seconds` | Maximum exponential or `Retry-After` delay. |

Needlr honors `Retry-After` for HTTP 429 and retries transport failures plus HTTP
500/502/503/504 only when the operation is provider-idempotent. Delays and request timeout are
caller-cancelable, and caller cancellation propagates with the caller's token.

### Trace queue and batch controls

`LangfuseOptions.TraceExport` configures the instrumented local processor:

| Option | Default | Description |
|---|---|---|
| `MaxQueueSize` | `2048` | Maximum completed activities held locally before new activities are dropped. |
| `ScheduledDelay` | `5 seconds` | Maximum wait before exporting a partial batch. |
| `MaxBatchSize` | `512` | Maximum activities in one OTLP request; cannot exceed the queue size. |
| `ExporterTimeout` | `30 seconds` | OTLP HTTP request timeout. |

All values are validated when an enabled integration starts. Queue admission and exporter results
feed `PublicationHealth`; the counters do not claim durable backend ingestion.

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

The built-in hosted REST clients use a named `IHttpClientFactory` pipeline. Configure it before
`AddNeedlrLangfuse` when the application needs a proxy, custom certificates, handlers, or connection
policy:

```csharp
builder.Services
    .AddHttpClient(LangfuseServiceCollectionExtensions.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => CreateLangfuseHandler());

builder.Services.AddNeedlrLangfuse();
```

This wires the OTLP exporter into the host's tracer and meter providers so they
share the application lifecycle, and registers the complete non-owning
`ILangfuseClient` facade:

```csharp
public sealed class MyEvalRunner(ILangfuseClient langfuse)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scenario = langfuse.BeginScenario(
            "support-agent: refund",
            sessionId: "ci-run-42",
            tags: ["regression"]);

        var response = await RunAgentAsync(cancellationToken);
        await scenario.RecordScoreAsync(
            "resolved",
            response.Resolved,
            options: null,
            cancellationToken);

        var experiment = langfuse.BeginExperimentRun(
            "support-agent-regression",
            "commit-abc123",
            new LangfuseExperimentRunOptions
            {
                Metadata = new { branch = "feature/refunds" },
            });
        var item = await experiment.RunItemAsync(
            "refund-case",
            async (scenario, token) =>
            {
                await scenario.RecordScoreAsync(
                    "reviewed",
                    value: true,
                    options: null,
                    token);
                return scenario.TraceId;
            },
            options: null,
            cancellationToken: cancellationToken);

        if (item.TraceId is { } traceId)
        {
            await langfuse.Scores.RecordScoreAsync(
                traceId,
                "published",
                value: true,
                options: null,
                cancellationToken);
        }

        Console.WriteLine(item.Link.Status);
        Console.WriteLine(experiment.GetPublicationSnapshot().ApiPublicationStatus);
    }
}
```

`ILangfuseClient` deliberately does not implement `IDisposable` and exposes no flush or
shutdown methods. The host owns the `TracerProvider`, optional `MeterProvider`, and shared
HTTP transport; disposing the host releases them in the correct order. No
`ILangfuseSession` is registered in dependency injection.

Every specialized interface (`ILangfuseScoreClient`, `ILangfuseDatasetClient`,
`ILangfuseScoreConfigClient`, `ILangfuseMetricsClient`, `ILangfuseModelClient`, and
`ILangfusePromptClient`) resolves to the exact instance exposed by the facade. When Langfuse
is not configured, the facade and all specialized interfaces are one coherent set of disabled
no-ops, so host code does not branch on credentials.

!!! note "DI override ownership"
    A custom unkeyed `ILangfuseClient` must be registered as a singleton **instance** before
    `AddNeedlrLangfuse`; its specialized clients are then exposed as externally owned instance
    aliases. Alternatively, individual specialized interfaces may be overridden as singletons
    and are composed into Needlr's facade. Scoped and transient overrides are rejected because a
    singleton facade cannot safely capture them. Keyed registrations are independent and remain
    untouched. The built-in `PublicationHealth` snapshot covers Needlr-owned exporters and REST
    clients; a custom specialized client owns the observability of its own publication path.

Run the no-server ownership check:

```bash
dotnet run --project src/Examples/AgentFramework/LangfuseConformanceApp -- dependency-injection
```

## Langfuse Cloud vs self-hosted

- **Cloud**: leave `Host` unset and set `Region` explicitly (`Eu`, `Us`, `Jp`, or `Hipaa`).
- **Self-hosted**: set `Host` to your deployment, e.g. `http://localhost:3000`.
  The OpenTelemetry endpoint requires Langfuse `v3.22.0` or newer.

One of `Host` or `Region` must be set — there is no default cloud target, so traces
are never sent to Langfuse Cloud unless you opt in.

Langfuse ingests OTLP over **HTTP** (`HTTP/protobuf`); gRPC is not supported, so the
exporter is always configured for HTTP.
