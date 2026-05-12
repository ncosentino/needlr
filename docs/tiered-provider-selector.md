---
description: Configure how TieredProviderSelector reacts to provider exceptions — match exception kinds, skip failing providers for a duration, run a callback on hit. Replaces the hardcoded ProviderUnavailableException catch with declarative, per-instance policies.
---

# Tiered Provider Selector

`TieredProviderSelector<TQuery, TResult>` iterates a priority-ordered list of `ITieredProvider` instances, falling through to the next provider when one fails. The fall-through behaviour is governed by `TieredProviderSelectorOptions.FailurePolicies` — a list of declarative match-and-react rules evaluated in order against each thrown exception.

The default options preserve the framework's historical behaviour (`ProviderUnavailableException` falls through with no skip, no callback). Consumers extend or replace the policies to handle their own exception types, cache failed providers across calls, and emit structured diagnostics on every hit.

This page covers the policy model, the cross-call skip cache, the OnHit callback contract, and a recipe for migrating from the legacy hardcoded behaviour.

---

## Why declarative policies?

Before this model, the selector hardcoded `catch (ProviderUnavailableException)` as the only fall-through-eligible exception. Three concrete problems followed:

1. **Wrapper boilerplate per provider.** A Copilot WebSearch provider that legitimately threw `CopilotAuthException` (or any HTTP-typed exception) had to be wrapped in a custom `ITieredProvider` whose `ExecuteAsync` caught and re-threw as `ProviderUnavailableException`. Every provider that fronted a third-party SDK paid the same boilerplate tax.
2. **No cross-call memory.** A provider that failed one call was retried first on the next call, even when its failure mode was clearly persistent (e.g., expired credentials). Every call paid the latency cost of the broken-but-tried-first provider.
3. **No diagnostic seam.** The `attempts` chain on `AllProvidersFailedException` carried prose; consumers wanting structured logs had to grep the message strings.

The policy model addresses all three: arbitrary exception types match by predicate, matching policies optionally cache the provider as skipped for a duration, and the optional `OnHit` callback fires with a typed `ProviderFailureContext` describing the failure.

---

## Quick example

Migrate a Copilot WebSearch provider that throws `CopilotAuthException` on bad credentials. The default behaviour is to propagate `CopilotAuthException` raw (because it is not `ProviderUnavailableException`); a policy makes it fall through to the next provider and skip the failing provider for 5 minutes.

The recommended path is to use the `AddTieredProviderSelector` DI extension (covered in detail in [DI registration](#di-registration) below). The `IServiceProvider`-aware overload lets the policy callbacks resolve other services (loggers, options monitors, telemetry sinks) from the container:

```csharp
using NexusLabs.Needlr.AgentFramework.Providers;

services.AddTieredProviderSelector<string, SearchResult>((sp, opts) =>
{
    var logger = sp.GetRequiredService<ILogger<CopilotWebSearchProvider>>();
    return opts with
    {
        FailurePolicies =
        [
            .. opts.FailurePolicies,
            new ProviderFailurePolicy(
                Match: ex => ex is CopilotAuthException,
                SkipDuration: TimeSpan.FromMinutes(5),
                OnHit: ctx =>
                {
                    logger.LogWarning(
                        ctx.Exception,
                        "Provider {Provider} skipped until {Until}",
                        ctx.ProviderName,
                        ctx.SkipUntil);
                    return ValueTask.CompletedTask;
                }),
        ],
    };
});
```

> **What you replace.** Most adopters previously embedded the catch-and-translate logic directly inside their `ITieredProvider.ExecuteAsync`:
>
> ```csharp
> // BEFORE — translation lives inside ExecuteAsync
> public async Task<SearchResult> ExecuteAsync(string query, CancellationToken ct)
> {
>     try { return await _client.SearchAsync(query, ct); }
>     catch (CopilotAuthException ex) { throw new ProviderUnavailableException(Name, ex.Message, ex); }
>     catch (HttpRequestException ex) { throw new ProviderUnavailableException(Name, ex.Message, ex); }
> }
>
> // AFTER — provider throws naturally; policy decides what to do
> public Task<SearchResult> ExecuteAsync(string query, CancellationToken ct) =>
>     _client.SearchAsync(query, ct);
> ```
>
> If you wrapped the translation in a separate decorator class instead of inlining it, the same migration applies — drop the decorator and let the underlying provider throw whatever it naturally throws.

The default policy (matching `ProviderUnavailableException`) is preserved by the spread `[.. opts.FailurePolicies, ...]`. Policies are evaluated in order, first match wins.

---

## The policy model

### `ProviderFailurePolicy`

```csharp
public sealed record ProviderFailurePolicy(
    Predicate<Exception> Match,
    TimeSpan? SkipDuration = null,
    Func<ProviderFailureContext, ValueTask>? OnHit = null);
```

| Field | Purpose |
|-------|---------|
| `Match` | Predicate evaluated against each thrown exception. The first policy whose `Match` returns `true` is applied; subsequent policies are not evaluated. |
| `SkipDuration` | Optional skip duration. See [Skip semantics](#skip-semantics) below. |
| `OnHit` | Optional async callback invoked after the skip cache is updated and before fall-through. See [OnHit contract](#onhit-contract) below. |

### `TieredProviderSelectorOptions`

```csharp
public sealed record TieredProviderSelectorOptions
{
    public IReadOnlyList<ProviderFailurePolicy> FailurePolicies { get; init; } = [];

    public static TieredProviderSelectorOptions Default { get; }
}
```

`Default` contains exactly one policy:

```csharp
new ProviderFailurePolicy(
    Match: ex => ex is ProviderUnavailableException,
    SkipDuration: null,
    OnHit: null)
```

This preserves the framework's pre-policy behaviour: `ProviderUnavailableException` falls through with no cross-call skip, every other exception type propagates raw. Consumers extend `Default` via the record `with` syntax — `Default` is itself an immutable singleton; cloning never mutates it.

---

## Skip semantics

`SkipDuration` controls whether the failing provider is cached as "skipped" so subsequent `ExecuteAsync` calls bypass it without an attempt:

| `SkipDuration` value | Behaviour |
|---|---|
| `null` | No cross-call skip. The provider is retried on the next call. |
| A finite `TimeSpan` (e.g. `TimeSpan.FromMinutes(5)`) | The provider is skipped until `now + SkipDuration`. Subsequent calls bypass it; once the duration elapses, the provider is attempted again. |
| `TimeSpan.MaxValue` (or `ProviderFailurePolicy.IndefiniteSkip`) | The provider is skipped until the host process restarts. Resolves to `DateTimeOffset.MaxValue` in the cache; the selector clamps the `now + duration` addition so this value cannot overflow. |

The skip cache is per-`TieredProviderSelector` instance — a `ConcurrentDictionary<string, DateTimeOffset>` keyed by provider name (`OrdinalIgnoreCase`). It is thread-safe and lives only in the host process; it is not persisted. Two selectors built from the same providers do not share skip state.

When a provider is bypassed because of a cached skip-until entry, the per-attempt diagnostic recorded in `AllProvidersFailedException.Attempts` says `"<ProviderName>: skipped until <ISO-timestamp>"` (or `"<ProviderName>: skipped indefinitely"` for the `DateTimeOffset.MaxValue` case).

### Time injection

The selector takes an optional `TimeProvider` parameter. Inject `Microsoft.Extensions.Time.Testing.FakeTimeProvider` in tests to drive deterministic skip-mode behaviour:

```csharp
var fakeTime = new FakeTimeProvider();
var selector = new TieredProviderSelector<string, string>(
    providers,
    new AlwaysGrantQuotaGate(),
    new AgentExecutionContextAccessor(),
    options: options,
    timeProvider: fakeTime);

await selector.ExecuteAsync("q1", CancellationToken.None);

fakeTime.Advance(TimeSpan.FromMinutes(6));

await selector.ExecuteAsync("q2", CancellationToken.None);
```

In production, the parameter defaults to `TimeProvider.System`.

---

## OnHit contract

`OnHit` is an optional async callback (`Func<ProviderFailureContext, ValueTask>`) invoked after the policy match is recorded but before the selector falls through to the next provider. It receives a typed `ProviderFailureContext`:

```csharp
public sealed record ProviderFailureContext(
    string ProviderName,
    Exception Exception,
    DateTimeOffset? SkipUntil);
```

| Field | Notes |
|---|---|
| `ProviderName` | The failing provider's `Name` property. |
| `Exception` | The exact exception thrown by `ITieredProvider.ExecuteAsync` and matched by the policy. |
| `SkipUntil` | The absolute UTC timestamp the provider will be skipped until — `null` when the policy specified `SkipDuration: null`, `DateTimeOffset.MaxValue` when the policy specified `IndefiniteSkip`. |

**Callback exceptions propagate.** If `OnHit` throws:

1. The selector still releases quota for the attempted provider (release happens in a `finally` block).
2. The callback's exception escapes `ExecuteAsync` and is thrown to the caller.
3. **Subsequent providers are not attempted** for that call — the policy chain short-circuits on callback failure.

This is intentional: a throwing `OnHit` is a programming error and surfacing it loudly is preferable to silently swallowing it. Wrap your callback bodies in `try`/`catch` if you want a different policy.

> **OnHit fires per failure, not per failure-burst.** If N concurrent calls to the same selector all hit a freshly-failed provider before any of them populate the skip cache, all N will invoke `OnHit`. The skip cache prevents subsequent waves but not the first wave — within a single concurrent burst, `OnHit` can fire up to N times for the same root failure. If your callback ships counters or alerts to a downstream system that doesn't tolerate duplicates (e.g., a Sentry-style "first occurrence" alert), dedupe at the consumer (e.g., a per-provider `MemoryCache` keyed on provider name + minute window).

---

## Default behaviour and migration

If your code does **not** pass `options` to the constructor, behaviour is unchanged from before this feature: `ProviderUnavailableException` falls through, every other exception type propagates raw, no cross-call memory.

To opt into cross-call skip for `ProviderUnavailableException` itself:

```csharp
var options = new TieredProviderSelectorOptions
{
    FailurePolicies =
    [
        new ProviderFailurePolicy(
            Match: ex => ex is ProviderUnavailableException,
            SkipDuration: TimeSpan.FromSeconds(30)),
    ],
};
```

(Note: this **replaces** the default policy rather than appending to it. Replacing is fine here because the default's `Match` predicate is identical — you are tightening it with a `SkipDuration`. To extend the default with an additional policy for a different exception type, use the spread pattern shown in [Quick example](#quick-example).)

---

## Quota release contract

A previous quota-leak bug (fixed alongside this feature) is worth calling out: `ReleaseAsync` now lives in a single `finally` block driven by a `succeeded` flag, so the quota gate is always released regardless of:

- Success path
- Matched-policy fall-through
- Unmatched-exception re-throw
- `OnHit` callback throwing

Previously, only the success path and the explicit `ProviderUnavailableException` catch released quota, so any other exception type silently leaked the reservation. Custom `IQuotaGate` implementations that count outstanding reservations no longer drift.

---

## Cancellation

When the active `CancellationToken` is cancelled, the selector skips policy evaluation entirely:

- `OperationCanceledException` propagates to the caller unchanged.
- No skip cache entry is added for the in-flight provider.
- Subsequent providers are not attempted.

If you genuinely want to treat cancellation as a skip-eligible failure, you cannot do it through the policy model — write a custom outer wrapper instead. The framework deliberately does not let policies match cancellation because the resulting timing is non-deterministic.

---

## See also

- `docs/providers.md` — the unrelated `[Provider]` typed-service-locator pattern.
- `docs/copilot.md` — the `CopilotAuthException` typed exception that motivated this feature.
- `src/Examples/AgentFramework/SimpleAgentFrameworkApp/Program.cs` — working example of `Default with { ... }` extension with a logging `OnHit` callback.

---

## DI registration

The recommended path for production hosts is the `AddTieredProviderSelector<TQuery, TResult>` extension method in `NexusLabs.Needlr.AgentFramework.Providers`. It wraps the boilerplate of resolving providers, the quota gate, the execution-context accessor, and `TimeProvider` from the container.

### Three overloads

```csharp
// 1. Defaults — TieredProviderSelectorOptions.Default, no customisation
services.AddTieredProviderSelector<WebSearchQuery, IReadOnlyList<WebSearchResult>>();

// 2. Simple options-only configure — for callbacks that don't need DI
services.AddTieredProviderSelector<WebSearchQuery, IReadOnlyList<WebSearchResult>>(
    opts => opts with { FailurePolicies = [...] });

// 3. DI-aware configure — when policy callbacks need ILogger, IOptionsMonitor, etc.
services.AddTieredProviderSelector<WebSearchQuery, IReadOnlyList<WebSearchResult>>(
    (sp, opts) =>
    {
        var logger = sp.GetRequiredService<ILogger<CopilotWebSearchProvider>>();
        return opts with { FailurePolicies = [...with logger captured...] };
    });
```

Pick the DI-aware overload (3) when your `OnHit` callback (or any other policy field) needs a logger, an `IOptionsMonitor`, a telemetry sink, or anything else from the container. The `configure` delegate runs lazily inside the singleton factory at first resolution, with a real `IServiceProvider` in scope, so you cannot accidentally capture a stale or test-only logger from your plugin's `Configure` scope. The delegate runs **exactly once per Singleton lifetime** — not once per `ExecuteAsync` call — so the cost of resolving services from `sp` inside `configure` is paid once.

### Lifetime and registration semantics

- **Singleton.** The selector's per-instance skip cache lives on the instance. Scoped or Transient would reset it per request, defeating the cross-call skip. The extension doesn't expose a knob for the lifetime — if you need a different one, hand-roll the registration.
- **Last-wins (override-friendly).** The extension uses `AddSingleton` (not `TryAddSingleton`). If two plugins both call `AddTieredProviderSelector<W, R>(...)` for the same `(TQuery, TResult)` pair, `GetRequiredService<ITieredProviderSelector<W, R>>()` returns the LAST registration's selector. This is intentional — it lets a downstream plugin or a test harness override an upstream plugin's selector without removing the prior registration first. (Both descriptors stay in the container; `GetServices` would return both. Almost no consumer enumerates the plural for a selector.)
- **`TimeProvider` registration is first-wins.** The extension calls `services.TryAddSingleton(TimeProvider.System)`. If the host already registered `TimeProvider` (ASP.NET Core / `Host.CreateApplicationBuilder()` does this automatically in .NET 8+), the existing registration is preserved. Test fixtures injecting `FakeTimeProvider` before calling the extension see their registration honoured.

### Configure delegate must not return `null`

If your `configure` lambda returns `null` (almost certainly a bug — a forgotten branch or an exception swallowed inside the lambda), the factory throws `InvalidOperationException` with a message pointing to `TieredProviderSelectorOptions.Default` as the correct value to return when you want framework defaults. Surfacing the bug loudly beats silently coalescing to `Default`.

### Escape hatch: hand-rolled factory

If you need something the extension doesn't expose (custom `QuotaPartitionSelector`, non-Singleton lifetime, custom `IServiceProvider` resolution strategy), the type is `[DoNotAutoRegister]` and the constructor accepts everything by parameter. Build the factory yourself:

```csharp
services.AddSingleton<ITieredProviderSelector<WebSearchQuery, IReadOnlyList<WebSearchResult>>>(sp =>
    new TieredProviderSelector<WebSearchQuery, IReadOnlyList<WebSearchResult>>(
        sp.GetServices<ITieredProvider<WebSearchQuery, IReadOnlyList<WebSearchResult>>>(),
        sp.GetRequiredService<IQuotaGate>(),
        sp.GetRequiredService<IAgentExecutionContextAccessor>(),
        partitionSelector: ctx => ctx?.Properties["TenantId"]?.ToString(),
        options: BuildOptions(sp),
        timeProvider: sp.GetRequiredService<TimeProvider>()));
```
