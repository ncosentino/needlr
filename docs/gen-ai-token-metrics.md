---
description: Emit cache_read and reasoning measurements on the OpenTelemetry gen_ai.client.token.usage histogram so dashboards can answer "what's my prompt-cache hit ratio?" and "how many tokens went to reasoning vs final completion?" per LLM call.
---

# GenAI Token Metrics

Needlr's `DiagnosticsChatClientMiddleware` emits `cache_read` and `reasoning` measurements on the OpenTelemetry **`gen_ai.client.token.usage`** histogram — the same histogram MEAI's [`OpenTelemetryChatClient`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient) emits `input` and `output` measurements on. The two writers cohabit cleanly so a single Prometheus / OTel-backed dashboard can slice token spend across all four `gen_ai.token.type` values per LLM call.

This is on by default whenever Needlr's diagnostics middleware is wired (via `UsingDiagnostics()`, `IIterativeAgentLoop`, or any custom path that constructs `DiagnosticsChatClientMiddleware` with the DI-provided `IGenAiTokenMetrics`).

---

## Why does this exist?

MEAI's `OpenTelemetryChatClient` follows the OpenTelemetry GenAI semantic conventions strictly. As of OTel semconv v1.41 (the version MEAI 10.5.0 implements), the `gen_ai.token.type` tag enum only allows `input` and `output` — there is no spec value for prompt-cache reads or reasoning tokens.

But [`UsageDetails`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.usagedetails) does carry that data:

- `UsageDetails.CachedInputTokenCount` — for providers that bill cached input tokens at a different rate (Anthropic prompt cache, Azure OpenAI cached input).
- `UsageDetails.ReasoningTokenCount` — for models that produce internal reasoning tokens billed separately from final output (o-series, Claude extended-thinking).

Operationally these are the two largest cost levers for those providers. Needlr surfaces them on the same histogram MEAI writes to so existing OTel pipelines pick them up without any additional plumbing.

---

## Quick start

```csharp
var serviceProvider = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .ConfigureMetrics(o => o.GenAiMeterName = "Experimental.Microsoft.Extensions.AI") // default
        .UsingDiagnostics())
    .BuildServiceProvider(configuration);
```

Then in your OpenTelemetry setup, ensure the meter is registered:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddMeter("Experimental.Microsoft.Extensions.AI") // captures BOTH MEAI and Needlr writes
        .AddPrometheusExporter());
```

That's it. Calls that hit the prompt cache or use reasoning tokens will produce additional histogram samples alongside MEAI's `input` / `output` samples.

---

## What gets emitted

Per successful LLM call (or per failed streaming call where partial usage was observed before the failure), `DiagnosticsChatClientMiddleware` records up to two samples on `gen_ai.client.token.usage`:

| Sample | `gen_ai.token.type` | Source |
|---|---|---|
| When `CachedInputTokenCount > 0` | `cache_read` | `UsageDetails.CachedInputTokenCount` |
| When `ReasoningTokenCount > 0` | `reasoning` | `UsageDetails.ReasoningTokenCount` |

**Skip-zero rule:** zero or negative counts emit nothing. The middleware short-circuits before resolving any tag metadata when both counts are zero (the common path), so the no-cache / no-reasoning case has near-zero overhead even when an OTel listener is attached.

**Anti-regression rule:** Needlr **never** emits `input` or `output` — those are MEAI's responsibility. Duplicate emission would silently double the recorded values.

---

## Tag schema

Each emitted sample carries the tags MEAI's [`AddMetricTags`](https://github.com/dotnet/extensions/blob/v10.5.0/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/OpenTelemetryChatClient.cs) attaches to the same histogram, so the resulting Prometheus series can be sliced the same way as MEAI's `input` / `output` series.

| Tag | Required? | Source |
|---|---|---|
| `gen_ai.token.type` | always | `"cache_read"` or `"reasoning"` |
| `gen_ai.operation.name` | always | `"chat"` (matches MEAI's `OpenTelemetryConsts.GenAI.ChatName`) |
| `gen_ai.provider.name` | always (value may be null) | `IChatClient.GetService<ChatClientMetadata>()?.ProviderName` |
| `gen_ai.request.model` | when set | `ChatOptions.ModelId`, falling back to `ChatClientMetadata.DefaultModelId` (matches MEAI's `options?.ModelId ?? metadata.DefaultModelId`) |
| `gen_ai.response.model` | when set | `ChatResponse.ModelId` |
| `server.address` | when set | `ChatClientMetadata.ProviderUri?.Host` |
| `server.port` | when address set | `ChatClientMetadata.ProviderUri?.Port` |

Tags whose source value is null are omitted from the recorded measurement, **with one exception**: `gen_ai.provider.name` is added unconditionally (with a null value when no provider metadata is available) because MEAI's `AddMetricTags` does the same — and label-set parity between the two writers is what allows Prometheus / the OpenTelemetry SDK to aggregate both into one series. `server.port` is always recorded whenever `server.address` is recorded — no scheme-default special-casing — again matching MEAI exactly.

`error.type` is **deliberately not** attached to Needlr's samples on the failure path, because MEAI also does not attach `error.type` to the token-usage histogram (only to the operation-duration histogram). Label-set parity between the two writers is what allows Prometheus to aggregate both into one series.

---

## Configuring the meter name

`AgentFrameworkMetricsOptions.GenAiMeterName` controls the meter Needlr uses. The default `"Experimental.Microsoft.Extensions.AI"` matches MEAI 10.5.0's `OpenTelemetryConsts.DefaultSourceName`, which is what `OpenTelemetryChatClient` uses when constructed without an explicit `sourceName`. If your composition root passes a custom `sourceName` to MEAI, set the same value here:

```csharp
.UsingAgentFramework(af => af
    .ConfigureMetrics(o => o.GenAiMeterName = "MyApp.GenAI"))
```

```csharp
// In your IChatClient pipeline:
services.AddSingleton<IChatClient>(sp => new OpenTelemetryChatClient(
    innerClient: realChatClient,
    sourceName: "MyApp.GenAI"));
```

For the OpenTelemetry SDK to combine Needlr's `cache_read` / `reasoning` measurements with MEAI's `input` / `output` measurements into a single metric stream, both writers must declare instruments with identical **stream identity**: meter name, instrument name, instrument type, unit, description, and bucket boundaries. Needlr's `GenAiTokenMetrics` matches MEAI exactly on every one of these — name `gen_ai.client.token.usage`, type `Histogram<int>`, unit `{token}`, description `"Measures number of input and output tokens used"`, explicit bucket boundaries `[1, 4, 16, 64, 256, 1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 16777216, 67108864]`. Runtime semantics differ deliberately (Needlr only emits `cache_read`/`reasoning`, skips zero counts, and clamps `long` token counts to `int.MaxValue`); instrument identity does not.

Setting only the meter name is sufficient — the rest is handled.

---

## Worked Prometheus query: cache hit ratio

```promql
sum(rate(gen_ai_client_token_usage_sum{gen_ai_token_type="cache_read"}[5m]))
  /
(
  sum(rate(gen_ai_client_token_usage_sum{gen_ai_token_type="input"}[5m]))
  + sum(rate(gen_ai_client_token_usage_sum{gen_ai_token_type="cache_read"}[5m]))
)
```

This computes "fraction of input tokens served from the provider's prompt cache" averaged over a 5-minute window. Slicing by `gen_ai.response.model` or `gen_ai.provider.name` shows hit ratio per model or per provider.

For reasoning-token spend share:

```promql
sum(rate(gen_ai_client_token_usage_sum{gen_ai_token_type="reasoning"}[5m]))
  /
sum(rate(gen_ai_client_token_usage_sum{gen_ai_token_type="output"}[5m]))
```

---

## Failure-path behaviour

For **non-streaming** failures (the inner `IChatClient.GetResponseAsync` throws before any response is returned), there is no usage data to record — neither MEAI nor Needlr emits anything on the token-usage histogram. The skip-zero rule in Needlr's middleware ensures this case emits nothing.

For **streaming** failures where `UsageContent` chunks arrived before the failure, `aggregated = buffered.ToChatResponse()` carries the partial usage data. In this case:

- MEAI's `OpenTelemetryChatClient` may emit `input` / `output` samples (its `TraceResponse` path inspects `response?.Usage` regardless of whether an error occurred) — though both the `input`/`output` measurements and Needlr's `cache_read`/`reasoning` measurements omit `error.type` on the token-usage histogram.
- Needlr's middleware emits `cache_read` / `reasoning` samples for whatever was observed before the failure, with the same skip-zero rule as the success path.

The result is that streaming partial-usage failures produce token-usage samples from **both** writers with consistent label sets, just as the success path does. Dashboard authors who filter by `error.type` on the operation-duration histogram (`gen_ai.client.operation.duration`) won't see token-usage samples from this path, since Needlr deliberately omits `error.type` to maintain label-set parity with the success path; if you need to attribute partial-usage failures, correlate the operation-duration `error.type` series with the token-usage series via `gen_ai.response.model` or a request-id label your application adds upstream.

---

## Spec drift note

`cache_read` and `reasoning` are not in the OpenTelemetry GenAI semantic-conventions `gen_ai.token.type` enum today. They are pragmatic extensions surfacing data that exists in `UsageDetails` but has no spec-defined token-type label. The two label values are centralised in the internal `GenAiTokenTypes` constants — if the OTel WG settles on different names later, only that file changes.

The instrument itself (`gen_ai.client.token.usage`, type, unit, description, buckets) follows the spec exactly and matches MEAI exactly, so spec compatibility is preserved at the instrument level.

---

## DI registration

`IGenAiTokenMetrics` is auto-registered when you call `UsingAgentFramework()`. The default implementation is `GenAiTokenMetrics`, owned as a singleton.

```csharp
// Auto-registered:
services.TryAddSingleton<IGenAiTokenMetrics>(sp =>
{
    var syringe = sp.GetService<BuiltAgentFrameworkSyringe>();
    var options = syringe?.Value.MetricsOptions ?? new AgentFrameworkMetricsOptions();
    return new GenAiTokenMetrics(options);
});
```

Mirrors `IAgentMetrics`'s registration shape — always-real (no NoOp variant). The .NET `Meter` runtime fast-paths `Histogram<T>.Record` to essentially zero work when no listeners are subscribed, so default-on has equivalent zero-overhead to a NoOp default.

---

## Related

- [Iterative Agent Loop](iterative-agent-loop.md) — wires the diagnostics middleware automatically when `UsingDiagnostics()` is called.
- [Pipeline Metrics](pipeline-metrics.md) — the sibling pipeline-shape metrics emitted by `SequentialPipelineRunner`.
