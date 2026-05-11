---
description: Pipeline-shape OpenTelemetry metrics and distributed-tracing spans emitted automatically by SequentialPipelineRunner — per-pipeline outcome, per-stage duration / tokens / tool-failures, parent + child activity spans.
---

# Pipeline Metrics

Needlr's `SequentialPipelineRunner` emits pipeline-shape OpenTelemetry metrics and distributed-tracing spans automatically. Pipeline-level dashboard questions like "which stage takes longest at p95?" or "where in the pipeline are tokens being spent?" can be answered without per-consumer telemetry plumbing.

This is opt-in by default (zero overhead when not configured). When you call `ConfigurePipelineMetrics(...)` on the agent-framework syringe, Needlr swaps in the real `PipelineMetrics` implementation and the runner starts emitting against your configured meter and activity-source name.

---

## Why pipeline-shape metrics?

[`IAgentMetrics`](iterative-agent-loop.md#di-registration) emits per-***agent-run*** series (`agent.run.*`, `agent.tool.*`, `agent.tokens.used`). MEAI's `OpenTelemetryChatClient` emits per-***LLM-call*** series. Neither covers the pipeline scope — `SequentialPipelineRunner` itself was observability-blind.

Operational questions like "which stage takes longest at p95" or "where in the pipeline are tokens being spent" can't be answered with `agent.*` series alone, because they aggregate across all stages an agent appears in. They need `stage_name`-keyed instruments, which can only come from the pipeline runner.

---

## Quick start

```csharp
// 1. Configure the pipeline meter when building the syringe.
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .ConfigureMetrics(o => o.MeterName = "MyApp.Agents")
        .ConfigurePipelineMetrics(o => o.MeterName = "MyApp.Pipelines"))
    .BuildServiceProvider(configuration);

// 2. Resolve the runner — IPipelineMetrics is injected automatically.
var runner = serviceProvider.GetRequiredService<SequentialPipelineRunner>();

// 3. Optionally name the pipeline (per-call) so the pipeline_name tag is meaningful.
var options = new SequentialPipelineOptions { PipelineName = "ArticlePipeline" };
var result = await runner.RunAsync(workspace, stages, options, cancellationToken);

// 4. Wire the meter + activity source into your OpenTelemetry pipeline.
//    See the OpenTelemetry section below for a full example.
```

When `ConfigurePipelineMetrics` is **not** called, the runner uses `NoOpPipelineMetrics` — every `Record*` method is a no-op and the exposed `ActivitySource` uses a dedicated `".NoOp"`-suffixed name so a listener targeting the canonical pipeline source name does not accidentally pick up no-op activities.

---

## The 7 instruments

All instruments emit on the configured `Meter` (defaults to `"NexusLabs.Needlr.AgentFramework.Pipelines"`).

### Pipeline-level (3)

| Instrument | Type | Tags | Description |
|---|---|---|---|
| `pipeline.run.started` | `Counter<long>` | `pipeline_name` | Incremented once at the start of every pipeline invocation. |
| `pipeline.run.completed` | `Counter<long>` | `pipeline_name`, `outcome` | Incremented once at the end of every pipeline invocation. `outcome` is `"Succeeded"` or `"Failed"`. |
| `pipeline.run.duration` | `Histogram<double>` (seconds) | `pipeline_name`, `outcome` | Wall-clock duration of the entire pipeline run. |

### Per-stage (4)

| Instrument | Type | Tags | Description |
|---|---|---|---|
| `pipeline.stage.completed` | `Counter<long>` | `pipeline_name`, `stage_name`, `outcome`, `termination_cause`, `phase_name` | Incremented once per stage (skipped, failed, succeeded, partial-failed) immediately after the stage's `IAgentStageResult` is constructed. `termination_cause` is `stage.Termination?.ToTagValue() ?? "Unspecified"`. `phase_name` is `"(none)"` for flat pipelines. |
| `pipeline.stage.duration` | `Histogram<double>` (seconds) | `pipeline_name`, `stage_name`, `outcome`, `phase_name` | Wall-clock duration of the stage's execution. **Not emitted for skipped stages** (no work was done). |
| `pipeline.stage.tokens` | `Counter<long>` | `pipeline_name`, `stage_name`, `token_kind` | One increment per **non-zero** token kind from `IAgentRunDiagnostics.AggregateTokenUsage`. `token_kind` is one of `input`, `output`, `cached_input`, `reasoning`. `total` is intentionally not emitted to avoid double-counting alongside `input + output`. Skipped stages emit nothing. |
| `pipeline.stage.tool.failed` | `Counter<long>` | `pipeline_name`, `stage_name`, `tool_name` | One increment per **failed** `ToolCallDiagnostics` in `stage.Diagnostics?.ToolCalls`. Succeeded calls emit nothing. Skipped stages emit nothing. |

---

## Tag schema

| Tag | Source | Cardinality |
|---|---|---|
| `pipeline_name` | `SequentialPipelineOptions.PipelineName` if set, else `IProgressReporter.WorkflowId` (which is `Guid.NewGuid().ToString("N")` by default). | Bounded by your distinct pipeline definitions if you set `PipelineName`. **Unbounded** (one per invocation) if you rely on the WorkflowId fallback — set `PipelineName` for production telemetry. |
| `stage_name` | `IAgentStageResult.AgentName` (the stage name registered when constructing `PipelineStage`). | Bounded by stage definitions. |
| `phase_name` | `IAgentStageResult.PhaseName` for phased pipelines (via `RunPhasedAsync`); `"(none)"` for flat. | Bounded by phase definitions. |
| `outcome` | `IAgentStageResult.Outcome.ToString()` for stage tags; `"Succeeded"` / `"Failed"` for pipeline tags. | 3 values: `Succeeded`, `Skipped`, `Failed`. |
| `termination_cause` | `stage.Termination?.ToTagValue() ?? "Unspecified"`. See [Stage Termination](stage-termination.md#totagvalue-opentelemetry-tag-values). | Bounded by the 11 framework `StageTermination` case names plus your `Custom.Reason` values. **Watch this**: if your `Custom.Reason` strings are high-cardinality, this tag will explode — bucket before constructing the `Custom` case, or drop the tag via `MeterView`. |
| `token_kind` | Hardcoded: `input`, `output`, `cached_input`, `reasoning`. | 4 values. |
| `tool_name` | `ToolCallDiagnostics.ToolName`. | Bounded by your registered tool count. |

### Setting `PipelineName`

`PipelineName` is per-call, on `SequentialPipelineOptions`:

```csharp
var options = new SequentialPipelineOptions
{
    PipelineName = "ArticlePipeline",  // pipeline_name tag value for this run
    TotalTokenBudget = 200_000,
    CompletionGate = result => result.Succeeded ? null : "draft missing",
};
await runner.RunAsync(workspace, stages, options, cancellationToken);
```

If `null`, the runner falls back to `IProgressReporter.WorkflowId` — which is a fresh `Guid` per invocation by default. **For production telemetry, always set `PipelineName`** so your dashboards can group runs by pipeline definition rather than by per-invocation GUID.

---

## Distributed tracing spans

Two activity types fire on the configured `ActivitySource` (defaults to the meter name):

### `pipeline.run` (parent span)

Created at the start of every pipeline invocation; disposed at every return path. Carries `pipeline_name` and `outcome` tags.

### `pipeline.stage` (child span)

Created per stage immediately after the `ShouldSkip` check (skipped stages emit metrics only — no span, because there's no work to trace). Carries `pipeline_name`, `stage_name`, `phase_name`, `outcome`, and `termination_cause` tags.

Activities are created via `ActivitySource.StartActivity(...)`. When no `ActivityListener` is registered, the call is essentially free (returns `null`) — the runner uses `using var` patterns and null-conditional access throughout, so the no-listener overhead is negligible.

---

## Configuration

### `ConfigurePipelineMetrics`

The fluent extension on `AgentFrameworkSyringe`:

```csharp
.UsingAgentFramework(af => af
    .ConfigureMetrics(o => o.MeterName = "MyApp.Agents")
    .ConfigurePipelineMetrics(o => o.MeterName = "MyApp.Pipelines"))
```

Subsequent calls mutate the same options instance via the record-with rebind — same semantics as `ConfigureMetrics`.

### `PipelineMetricsOptions`

| Property | Type | Default | Purpose |
|---|---|---|---|
| `MeterName` | `string` | `"NexusLabs.Needlr.AgentFramework.Pipelines"` | The `Meter` name. Set to your dashboard's keyed name (e.g. `"MyApp.Pipelines"`) so your existing OTel views and Prometheus aggregations pick it up without configuration. |
| `ActivitySourceName` | `string?` | `null` (falls back to `MeterName`) | The `ActivitySource` name for `pipeline.run` and `pipeline.stage` spans. Set independently if your tracing pipeline filters by source name. |

Why a separate options class from `AgentFrameworkMetricsOptions`? Most consumers want pipeline metrics under a separate meter (e.g. `"MyApp.Pipelines"`) so OTel views and Prometheus aggregation can target each scope independently. Coupling them would force one meter for both scopes.

---

## OpenTelemetry wiring

Add the configured meter and activity source to your OTel pipeline. Both are picked up automatically when listeners attach:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddMeter("MyApp.Agents")
        .AddMeter("MyApp.Pipelines")
        .AddPrometheusExporter())
    .WithTracing(b => b
        .AddSource("MyApp.Agents")
        .AddSource("MyApp.Pipelines")
        .AddOtlpExporter());
```

Wildcard meter names work for the metrics side (`AddMeter("MyApp.*")`) but `ActivitySource` requires explicit source names — there's no `AddSource("MyApp.*")` API. List both source names explicitly.

---

## Cardinality discipline

The `pipeline.stage.completed` instrument carries 5 tags. Cardinality is roughly:

```
pipeline_count × stage_count × outcome × termination_cause × phase_count
```

For a 23-stage pipeline with 12 termination causes, 3 outcomes, and 1 pipeline name, that's already ~828 series per pipeline. Acceptable for most OTel backends (Prometheus, Mimir, Cortex) but worth being deliberate about.

The two cardinality risks worth calling out:

1. **`termination_cause` from unbounded `Custom.Reason` strings.** If your `onLoopCompleted` callbacks return `new StageTermination.Custom(reason: $"User {userId} failed")`, the cardinality of `termination_cause` becomes the cardinality of your user IDs. Bucket before constructing the case, or drop the tag via OTel `MeterView`.
2. **`pipeline_name` from the WorkflowId fallback.** If you don't set `PipelineName` on `SequentialPipelineOptions`, every invocation gets a fresh `Guid` for the tag — cardinality grows linearly with invocations. **Always set `PipelineName`** for production telemetry.

For dropping a tag via OTel views:

```csharp
.WithMetrics(b => b
    .AddView(instrumentName: "pipeline.stage.completed", new MetricStreamConfiguration
    {
        TagKeys = ["pipeline_name", "stage_name", "outcome", "phase_name"],
        // termination_cause dropped from this view
    }))
```

---

## Migration: replacing per-consumer pipeline-metrics impls

If you previously maintained a per-consumer `PipelineMetrics` class that wired up its own `Meter`, instruments, and recording calls from your runner wrapper:

**Before** — bespoke metrics class + recording wiring in your pipeline runner wrapper:

```csharp
internal sealed class PipelineMetrics
{
    private readonly Meter _meter = new("MyApp.Pipelines");
    private readonly Counter<long> _runs;
    private readonly Histogram<double> _runDuration;
    // ... ~180 lines of instrument creation + RecordRun + RecordStage + ...
}

// In the pipeline runner wrapper:
_pipelineMetrics.RecordRun(...);
_pipelineMetrics.RecordStage(stageName, terminationReason, ...);
```

**After** — opt into the framework-emitted metrics + spans by configuring once in DI:

```csharp
.UsingAgentFramework(af => af
    .ConfigurePipelineMetrics(o => o.MeterName = "MyApp.Pipelines"))
```

Delete the bespoke metrics class. Delete the `RecordRun` / `RecordStage` calls in the wrapper. Re-point dashboard queries to the new instrument names (`pipeline.run.*`, `pipeline.stage.*`) — the tag schema is documented above.

If the bespoke metrics class implemented bucketing for a stringly-typed termination reason, that helper deletes too: `termination_cause` is now `Termination?.ToTagValue()`, which is cardinality-safe by default for framework cases. Only `Custom.Reason` strings need consumer-side bucketing.

---

## Migration: `SequentialPipelineRunner` constructor

The runner constructor takes a 4th parameter `IPipelineMetrics`:

- **DI-resolved consumers see no break** — Needlr's `RegisterAgentFrameworkInfrastructure` registers `IPipelineMetrics` automatically. Without `ConfigurePipelineMetrics`, the registration resolves to `NoOpPipelineMetrics` (zero overhead).
- **Manual constructions** (test fixtures, examples) need to pass `IPipelineMetrics` explicitly:

```csharp
// Manual construction with the no-op default:
var runner = new SequentialPipelineRunner(
    diagnosticsAccessor,
    budgetTracker,
    progressReporterFactory,
    new NoOpPipelineMetrics());

// Or resolve from DI:
var runner = new SequentialPipelineRunner(
    diagnosticsAccessor,
    budgetTracker,
    progressReporterFactory,
    serviceProvider.GetRequiredService<IPipelineMetrics>());
```

---

## See also

- [Stage Termination](stage-termination.md) — typed `StageTermination` hierarchy, the source of the `termination_cause` tag value.
- [Iterative Agent Loop](iterative-agent-loop.md) — `IIterativeAgentLoop` and `IAgentMetrics`, the per-agent-run companion to pipeline-shape metrics.
