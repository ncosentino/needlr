---
description: Typed-case StageTermination hierarchy describes why a pipeline stage stopped — pattern-match in C#, low-cardinality OTel tags via ToTagValue, polymorphic JSON wire format.
---

# Stage Termination

`StageTermination` is the typed answer to "why did this stage stop." It surfaces on `IAgentStageResult.Termination` and `StageExecutionResult.Termination` after every stage the runner emits, with structured per-case metadata that consumers can pattern-match on or render as low-cardinality OpenTelemetry tag values.

It exists alongside `StageOutcome` (the rollup 3-value `Succeeded` / `Skipped` / `Failed` enum the runner decided about the stage). They carry overlapping but distinct signal — see [`StageOutcome` vs `Termination`](#stageoutcome-vs-termination) below.

The `StageTermination` hierarchy itself is **closed for external derivation at compile time** — the abstract record's constructor is `internal`, so consumer code cannot inherit from it. Extension happens by implementing the [`IStageTermination`](#extending-istagetermination) interface directly. The framework's typed cases stay exhaustive; consumer-defined cases are first-class peers of the framework cases through the interface contract.

---

## Why typed?

Before `StageTermination`, consumers wanting more resolution than `Outcome.Succeeded / Skipped / Failed` had to carry a parallel `Dictionary<string, string>` accessor mutated from `IterativeLoopStageExecutor.onLoopCompleted` callbacks. That worked but:

- Programmatic switching required `string.StartsWith("MaxIterationsReached", …)` — the same anti-pattern Needlr replaced for `NoProvidersAvailableException` with typed exceptions.
- Structured metadata was stringified into prose (`"MaxIterationsReached after 7 iterations"`), invisible to dashboards and brittle to wording changes.
- Every consumer reinvented their own cardinality-safe bucketing helper to convert the free-form strings into bounded Prometheus tag values.

Typed cases solve all three: pattern-match against named record types, named-field property access, default low-cardinality `ToTagValue()` for tag values.

---

## Quick example

```csharp
// In an IterativeLoopStageExecutor.onLoopCompleted callback — return a Custom case
// to attach app-specific narrative + structured metadata, or null to defer to the
// framework-mapped default.
new IterativeLoopStageExecutor(
    iterativeLoop,
    optionsFactory: ctx => buildOptions(ctx),
    onLoopCompleted: (loopResult, ctx) =>
    {
        var findingCount = pipelineRunAccessor.ColdReaderFindings.Count;
        return new StageTermination.Custom(
            Reason: "Reconciled",
            Properties: new Dictionary<string, object?>
            {
                ["FindingCount"] = findingCount,
            });
    });

// On the consumer side, after the pipeline runs:
foreach (var stage in result.Stages)
{
    if (stage.Termination is StageTermination.MaxToolCallsReached { Limit: var limit })
    {
        _logger.LogWarning("Stage {Name} exceeded {Limit} tool calls",
            stage.AgentName, limit);
    }
    else if (stage.Termination is StageTermination.Custom { Reason: "Reconciled", Properties: { } props }
             && props.TryGetValue("FindingCount", out var n))
    {
        _logger.LogInformation("Stage {Name} reconciled with {Count} findings",
            stage.AgentName, n);
    }
}
```

---

## The 12 cases

`StageTermination` is an `abstract record` with 12 nested `sealed record` cases. They split into four groups by where the termination is detected:

### Loop-natural terminations

| Case | Fields | When it fires |
|---|---|---|
| `Completed` | — | The `IsComplete` predicate returned `true` after an iteration. |
| `NaturalCompletion` | — | The model produced a text response without requesting tool calls. |
| `CompletedEarlyAfterToolCall` | — | The `IsComplete` predicate returned `true` *during* an iteration after a tool call. The loop exited before a wasted chat completion. Only fires when `CheckCompletionAfterToolCalls` is `AfterToolRounds` or `AfterEachToolCall`. |

### Loop-bounded terminations

| Case | Fields | When it fires |
|---|---|---|
| `MaxIterationsReached(int Limit, int IterationsUsed)` | `Limit`, `IterationsUsed` | The loop ran `Limit` iterations without `IsComplete` returning `true`. `IterationsUsed` is taken from `loopResult.Iterations.Count`. |
| `MaxToolCallsReached(int Limit, int ToolCallsUsed)` | `Limit`, `ToolCallsUsed` | The cumulative tool-call count across iterations exceeded `MaxTotalToolCalls`. `ToolCallsUsed` is summed from `IterationRecord.ToolCallCount` across all iterations. |
| `BudgetPressure(double? Threshold)` | `Threshold` | The token budget tracker reported usage above threshold and the loop ran one final finalization iteration. `Threshold` is nullable because not every consumer configures one explicitly. |
| `StallDetected(int? ConsecutiveThreshold)` | `ConsecutiveThreshold` | Stall detection fired (consecutive iterations with similar token counts). `ConsecutiveThreshold` is nullable because the configuration snapshot is itself optional. |

### External terminations

| Case | Fields | When it fires |
|---|---|---|
| `Cancelled` | — | The loop's `CancellationToken` was triggered. |
| `Failed(Exception Exception)` | `Exception` | The stage threw, OR the iterative loop reported `TerminationReason.Error` (in which case the loop's `ErrorMessage` is wrapped in a fresh `InvalidOperationException` — see [Failed unifies stage-throw and loop-Error](#failed-unifies-stage-throw-and-loop-error)). |

### Stage-level terminations

| Case | Fields | When it fires |
|---|---|---|
| `Skipped(string? Reason = null)` | `Reason` | The runner's `ShouldSkip` policy predicate returned `true`. `Reason` is nullable because `ShouldSkip` is currently a `Func<…, bool>` with no reason channel; future enrichment can make it required. |

### Escape hatch

| Case | Fields | When it fires |
|---|---|---|
| `Custom(string Reason, IReadOnlyDictionary<string, object?>? Properties = null)` | `Reason`, optional `Properties` | App-specific narrative not covered by framework cases. Returned from an `onLoopCompleted` callback to attach a domain-specific termination cause (`"Reconciled"`, `"BudgetExceededByPolicyDecision"`, etc.) plus optional structured metadata. |

---

## `ToTagValue()` — OpenTelemetry tag values

Every `StageTermination` exposes a `virtual string ToTagValue()` that returns a stable, low-cardinality string suitable for OpenTelemetry / Prometheus tag values:

- **Framework cases** return the case name via `GetType().Name` — so `MaxIterationsReached`, `Cancelled`, `BudgetPressure`, etc. produce literally those strings. There are 11 framework case names total, so the cardinality contribution is bounded by the case enumeration.
- **`Custom`** overrides `ToTagValue()` to return its `Reason` field directly — so `new Custom("Reconciled")` produces `"Reconciled"`. Consumers with high-cardinality `Custom.Reason` strings (user IDs, timestamps, free-form messages) are responsible for bucketing before they record the tag, or for using OTel `MeterView` to drop the tag entirely.

Pipeline-shape metrics use this automatically as the `termination_cause` tag — see [Pipeline Metrics](pipeline-metrics.md) for the full tag schema.

```csharp
string tag = stage.Termination?.ToTagValue() ?? "Unspecified";
metrics.RecordStageOutcome(tag);
```

---

## Extending: `IStageTermination`

The framework's `StageTermination` hierarchy is closed for external derivation by design — the abstract record's constructor is `internal` so external `: StageTermination` is a compile error. The 11 framework typed cases plus `Custom` are exhaustive for the runner's needs.

Consumers who want their own typed extension case (with named-record pattern matching like `is MyDomainTermination { FindingCount: var c }`) implement the **`IStageTermination`** interface directly:

```csharp
public sealed record MyDomainTermination(int FindingCount) : IStageTermination
{
    public string ToTagValue() => "MyDomain";
}
```

`IAgentStageResult.Termination` is declared as `IStageTermination?`, so a third-party impl flows through the runner and pipeline metrics seamlessly. Pattern matching against your own type works exactly as it does for framework cases:

```csharp
foreach (var stage in result.Stages)
{
    if (stage.Termination is MyDomainTermination { FindingCount: var n })
    {
        _logger.LogInformation("Stage reconciled with {Count} findings", n);
    }
}
```

### When to use which

- **Use `StageTermination.Custom`** when you don't need named-record pattern matching and want zero JSON-serialisation work. Wraps your termination data into the `Reason` string + `Properties` dictionary; round-trips cleanly via the framework's polymorphism registry.
- **Use a custom `IStageTermination` impl** when you need typed pattern matching, structured-but-typed properties (not stringified into a dictionary), or richer per-case semantics. **You own JSON serialization** — see below.

### Third-party JSON contract

Implementing `IStageTermination` is a contract: **you handle JSON serialization for your derived type**. The framework's `[JsonPolymorphic]` registry on the interface only knows about the framework cases — your type is not in it. `JsonSerializer.Serialize<IStageTermination>(yourInstance)` throws `NotSupportedException` until you register your type.

Two practical options:

#### Option A — `JsonTypeInfoResolver` modifier (lightweight)

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver
    {
        Modifiers =
        {
            info =>
            {
                if (info.Type == typeof(IStageTermination)
                    && info.PolymorphismOptions is { } poly)
                {
                    poly.DerivedTypes.Add(new JsonDerivedType(
                        typeof(MyDomainTermination), "MyDomainTermination"));
                }
            },
        },
    },
};

var json = JsonSerializer.Serialize<IStageTermination>(myInstance, options);
// {"$kind":"MyDomainTermination","FindingCount":7}
```

Your derived type joins the polymorphism table for that `JsonSerializerOptions` instance. The `$kind` discriminator stays uniform across framework + third-party cases. Apply the modifier to **every** `JsonSerializerOptions` your code uses to serialise `StageTermination` results — easy to forget, but the failure mode (`NotSupportedException`) is loud.

#### Option B — custom `JsonConverter<IStageTermination>` (full control)

For consumers who need to fully own the wire format (not just register a discriminator), write a custom converter:

```csharp
public sealed class MyStageTerminationConverter : JsonConverter<IStageTermination>
{
    public override IStageTermination? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // ... full control: choose discriminator field, fallback behaviour, etc.
    }

    public override void Write(
        Utf8JsonWriter writer, IStageTermination value, JsonSerializerOptions options)
    {
        // ...
    }
}
```

Heavier than the modifier path but gives you complete wire-format control — useful when you have multiple consumer-defined types and a consistent registration scheme, or when you need to bridge to a non-`$kind` discriminator convention used elsewhere in your serialised payloads.

### What if you do nothing?

`JsonSerializer.Serialize<IStageTermination>(myUnregisteredInstance)` throws `NotSupportedException`. Same loud failure the framework cases have today for unregistered types — **no silent data loss**. The contract is: implement the interface, take on the JSON registration. If that's too much work, fall back to `StageTermination.Custom`.

### See an example

`src/Examples/AgentFramework/RfcPipelineApp` demonstrates the `JsonTypeInfoResolver` modifier path end-to-end: a custom `IStageTermination` returned from an `onLoopCompleted` callback, registered against `JsonSerializerOptions`, and round-tripped through `JsonSerializer` to prove the wire format works.

---

## `StageOutcome` vs `Termination`

These two members on `IAgentStageResult` carry overlapping but **intentionally distinct** signal:

- **`Outcome`** is the rollup 3-value enum the runner decided about the stage: `Succeeded` / `Skipped` / `Failed`. Use this for high-level dashboard rollups (success rate, failure rate).
- **`Termination`** is the typed cause the loop or runner produced. Use this for granular pattern matching, per-cause telemetry, and post-mortem analysis.

The decoupling matters most when `IterativeLoopStageExecutor.shouldTreatAsSuccess` flips a non-success loop result into a successful stage outcome. For example, a loop that hits its iteration cap normally produces `Outcome.Failed`, but a consumer that treats `MaxIterationsReached` as acceptable can supply `shouldTreatAsSuccess: r => r.Termination is TerminationReason.MaxIterationsReached`. The executor then reports:

| Field | Value |
|---|---|
| `Outcome` | `Succeeded` (flipped by `shouldTreatAsSuccess`) |
| `Termination` | `StageTermination.MaxIterationsReached(Limit: 25, IterationsUsed: 25)` (still reflects what the loop actually did) |

This is locked in by `ExecuteAsync_ShouldTreatAsSuccess_KeepsTerminationFromLoop` in the executor tests.

---

## Failed unifies stage-throw and loop-Error

`StageTermination.Failed(Exception)` is the unified case for two distinct paths:

1. **Stage-level throw.** An executor (custom or built-in) threw. The runner's catch handler synthesizes `new StageTermination.Failed(theCaughtException)` with the original exception preserved.
2. **Loop-level error.** `IIterativeAgentLoop` reported `TerminationReason.Error` with an `ErrorMessage` string. The loop's contract carries only a string here, not an exception, so `IterativeLoopStageExecutor` wraps the message in a fresh `InvalidOperationException` to fit the `Failed(Exception)` shape: `new StageTermination.Failed(new InvalidOperationException(loopResult.ErrorMessage ?? "loop reported error"))`.

This loses one bit of fidelity ("did this come from a stage throw or from a loop error?") in exchange for a tighter API surface. Consumers needing the distinction can inspect the exception type and message pattern.

---

## JSON wire format

`StageTermination` participates in `System.Text.Json` polymorphic serialization via:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(Completed), nameof(Completed))]
[JsonDerivedType(typeof(MaxIterationsReached), nameof(MaxIterationsReached))]
// ... per-case [JsonDerivedType] entries ...
```

Discriminator values use `nameof()` so refactor renames cannot silently change the wire format. **Once shipped, the JSON wire format is part of the public API surface** — renaming or removing a case breaks downstream consumers that deserialize on the other end of an HTTP boundary or a job-queue message.

Round-trip example:

```json
{
  "$kind": "MaxIterationsReached",
  "Limit": 10,
  "IterationsUsed": 7
}
```

### `Custom.Properties` JSON caveat

`Custom.Properties` is typed as `IReadOnlyDictionary<string, object?>` for flexibility, but `System.Text.Json` round-trips `object?` values as `JsonElement` — not the original concrete types. A `Properties["FindingCount"] = 4` serialised and deserialised yields a `JsonElement` (numeric) on the other end, not an `int`.

Consumers needing type-safe per-property access should call `JsonElement.Deserialize<T>()` per value:

```csharp
if (custom.Properties is { } props && props.TryGetValue("FindingCount", out var raw))
{
    var count = raw is JsonElement element
        ? element.Deserialize<int>()
        : (int)raw!;
}
```

For applications where this matters, a typed sub-record under `Custom` (or a dedicated typed termination case) is cleaner than a string-keyed dictionary.

---

## Adding new framework cases later

Adding a new framework case in a future Needlr release is a **soft break** for consumers using exhaustive switch *expressions*:

```csharp
// Soft-breaks if a new framework case is added later — compile error on the missed arm.
string label = stage.Termination switch
{
    StageTermination.Completed => "OK",
    StageTermination.MaxIterationsReached => "OverIterations",
    // ... 10 more arms ...
    null => "—",
};
```

Switch *statements* without a return value continue to work without modification. Switch expressions can defend by adding a `_ => "Unknown"` default arm. Document policy: new framework cases will be released as minor-version additions.

---

## Migration: replacing parallel-dictionary workarounds

If your consumer maintains a parallel `ConcurrentDictionary<string, string>` accessor populated from `IterativeLoopStageExecutor.onLoopCompleted`, you can delete it in favour of returning typed `Custom` cases from the callback:

**Before** — accessor map plumbed through factories and read in the runner:

```csharp
// In IArticlePipelineRunAccessor:
IReadOnlyDictionary<string, string> StageTerminationReasons { get; }
void SetStageTerminationReason(string stageName, string? reason);

// In every stage factory's onLoopCompleted callback:
onLoopCompleted: (loopResult, ctx) =>
{
    pipelineRunAccessor.SetStageTerminationReason(
        ctx.StageName,
        $"Completed — {findingCount} finding(s) recorded");
};

// In the runner that reads results:
foreach (var stage in result.Stages)
{
    var reason = pipelineRunAccessor.StageTerminationReasons.GetValueOrDefault(stage.AgentName);
    // ... pass to Loki / Prometheus / job result JSON ...
}
```

**After** — typed `Custom` cases on the result itself:

```csharp
// In every stage factory's onLoopCompleted callback:
onLoopCompleted: (loopResult, ctx) => new StageTermination.Custom(
    Reason: "Completed",
    Properties: new Dictionary<string, object?> { ["FindingCount"] = findingCount });

// In the runner that reads results:
foreach (var stage in result.Stages)
{
    var reason = stage.Termination?.ToTagValue() ?? "Unspecified";
    // ... pass to Loki / Prometheus / job result JSON ...
}
```

The accessor interface, holder, and ConcurrentDictionary all delete. The factory callback signature and call sites simplify. Pipeline-shape metrics already pick up the `termination_cause` tag automatically — no separate bucketing helper is needed because `ToTagValue()` is cardinality-safe by default.

---

## Migration: `IterativeLoopStageExecutor.onLoopCompleted` signature

The callback signature changed from `Action<IterativeLoopResult, StageExecutionContext>?` to `Func<IterativeLoopResult, StageExecutionContext, StageTermination?>?`. Existing `Action`-shaped lambdas need `return null;` added at the end:

```csharp
// Before
onLoopCompleted: (result, ctx) =>
{
    accessor.SetReason(ctx.StageName, result.Termination.ToString());
}

// After (minimal migration — defer to framework-mapped default)
onLoopCompleted: (result, ctx) =>
{
    accessor.SetReason(ctx.StageName, result.Termination.ToString());
    return null;  // null = use framework-mapped default StageTermination
}

// After (preferred — replace the parallel accessor with a typed Custom case)
onLoopCompleted: (result, ctx) => new StageTermination.Custom(
    Reason: result.Termination.ToString(),
    Properties: null);
```

---

## See also

- [Iterative Agent Loop](iterative-agent-loop.md) — `IIterativeAgentLoop`, the source of `TerminationReason` enum values that map into `StageTermination` cases.
- [Pipeline Metrics](pipeline-metrics.md) — pipeline-shape OTel metrics + tracing that consume `Termination?.ToTagValue()` as the `termination_cause` tag automatically.
