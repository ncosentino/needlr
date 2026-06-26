---
description: Source-generate a registration of an open generic composition type closed over the type argument of every discovered implementation of another open generic interface, exposed as a non-generic facade -- source generation only.
---

# Compose and Expose Closed Generics

`[RegisterClosedOverImplementationsOf]` lets you keep a **compose-and-expose** pattern on the source-generation path. For **each** discovered concrete closed implementation of a designated open generic interface, the generator closes a reusable composition type over the *same* type argument(s) and registers it as a designated facade service type — resolving the composition's constructor dependencies from DI. This is a **source-generation-only** feature.

## Overview

A common pattern is an open, growing set of typed "definitions" — each a small type implementing `IFooDefinition<TData>` — that you want to expose uniformly behind a non-generic facade `IFoo`, so the rest of the system can enumerate `IEnumerable<IFoo>` without knowing any `TData`. A reusable open generic `FooCore<TData>` *composes* a definition with other per-type services (no inheritance) and implements `IFoo`.

Without this feature you either:

- **Hand-maintain one registration per `TData`** — which silently drifts: add a definition and forget the matching line, and the new variant is simply absent at runtime with no compile-time signal; or
- **Use runtime reflection** (`Assembly.GetTypes()` + `MakeGenericType` + `ActivatorUtilities`) — which is AOT/trimming hostile and against the source-generation-first design.

With `[RegisterClosedOverImplementationsOf]`, you write the marker once and adding a new feature variant means **adding one definition** and nothing else.

## Quick Start

```csharp
using NexusLabs.Needlr.Generators;

// Auto-discovered today: concrete, closed, unattributed.
public interface IFooDefinition<TData> where TData : class
{
    string Discriminator { get; }
}

public sealed class AlphaFoo : IFooDefinition<AlphaData> { public string Discriminator => "alpha"; }
public sealed class BetaFoo  : IFooDefinition<BetaData>  { public string Discriminator => "beta";  }

// Non-generic facade consumed as IEnumerable<IFoo>.
public interface IFoo { string Discriminator { get; } }

// Reusable composition, closed per discovered TData and exposed as IFoo.
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
public sealed class FooCore<TData> : IFoo where TData : class
{
    public FooCore(IFooDefinition<TData> definition, IFooStore<TData> store) { /* ... */ }
    public string Discriminator => /* delegates to definition */ "";
}
```

## What Gets Generated

At compile time, Needlr discovers every concrete closed implementation of `IFooDefinition<>` and emits one registration per discovered type argument, resolving each constructor dependency closed over the same `TData`:

```csharp
// Generated code (simplified)
services.AddSingleton<IFoo>(sp => new FooCore<AlphaData>(
    sp.GetRequiredService<IFooDefinition<AlphaData>>(),
    sp.GetRequiredService<IFooStore<AlphaData>>()));

services.AddSingleton<IFoo>(sp => new FooCore<BetaData>(
    sp.GetRequiredService<IFooDefinition<BetaData>>(),
    sp.GetRequiredService<IFooStore<BetaData>>()));
```

The generator reads the composition's constructor and resolves **whatever** it asks for — it does not need to know about stores, loggers, or any specific dependency. `[FromKeyedServices("key")]` constructor parameters are resolved as keyed services.

## Lifetime

Each registration defaults to `Singleton`. Use the `Lifetime` property to change it:

```csharp
[RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo), Lifetime = InjectableLifetime.Scoped)]
public sealed class FooCore<TData> : IFoo where TData : class { /* ... */ }
```

## Multi-Parameter Generics

The marker takes the unbound `typeof(IFooDefinition<>)`, and the composition is closed over the discovered type argument(s) as a list. Interfaces with multiple type parameters work the same way, provided the composition's arity matches:

```csharp
public interface IPairDefinition<TKey, TValue> where TKey : class where TValue : class { }

[RegisterClosedOverImplementationsOf(typeof(IPairDefinition<,>), As = typeof(IPair))]
public sealed class PairCore<TKey, TValue> : IPair
    where TKey : class where TValue : class
{
    public PairCore(IPairDefinition<TKey, TValue> definition) { /* ... */ }
}
```

## Constraint Validation

If a discovered type argument does not satisfy the composition type's generic constraints, the registration is **skipped** and the generator reports [NDLRGEN038](analyzers/NDLRGEN038.md) at build time — turning a would-be runtime absence into a build-time signal.

This diagnostic is only *reachable* when the composition's constraints are **stricter** than the source interface's. If both carry the same constraint (e.g. `where TData : class`), every discovered implementation can always close the composition, so the diagnostic acts as a safety net for future divergence rather than something you hit day one.

## What This Does and Does Not Catch

The feature eliminates the **"I added a definition and forgot to wire it"** drift entirely: valid definitions auto-wire.

It does **not** make the whole dependency graph statically provable. Transitive dependencies that the closed composition needs — such as an open-generic store registered in a plugin, or `ILogger<>` from `AddLogging` — are not visible to the source generator. If such a dependency is missing, resolution still throws at runtime (the same behavior as a hand-written factory). There is no regression; the failure simply remains a runtime one.

## Cross-Assembly Discovery

A composition closes over **exactly the set of definitions the generator registers** — the current assembly plus any referenced **plain class libraries** (libraries without their own `[GenerateTypeRegistry]`). This is the same set `[OpenDecoratorFor]` expands over, so cross-assembly definitions in plain libraries are handled with no extra work.

The one boundary is Needlr's universal multi-assembly model: a referenced assembly that carries its **own** `[GenerateTypeRegistry]` self-registers its types (and is force-loaded by the consumer), so the consuming assembly's generator does not re-scan it. A marker in assembly **A** therefore does not reach definitions inside a self-registering referenced assembly **B** — **B** owns its own registration and can carry its own marker. Keep the marker in the same assembly as its definitions, or expose the definitions from a plain library, and discovery spans assemblies transparently.

## Comparison with `[OpenDecoratorFor]`

| Feature | `[OpenDecoratorFor]` | `[RegisterClosedOverImplementationsOf]` |
|---------|----------------------|------------------------------------------|
| Closes an open generic per discovered implementation | ✅ | ✅ |
| Registered service type | the **same** closed interface (wraps the inner) | a **different** facade named by `As` |
| Reads the closed type's constructor | ❌ (uses `AddDecorator`) | ✅ (emits `new T(...)` with DI resolution) |
| Intent | decorate / wrap | compose and expose behind a facade |
| Works with source-gen | ✅ | ✅ |
| Attribute location | `NexusLabs.Needlr.Generators` | `NexusLabs.Needlr.Generators` |

## Attribute Reference

| Member | Type | Default | Description |
|--------|------|---------|-------------|
| `SourceOpenGenericInterface` (ctor) | `Type` | — | The open generic interface whose concrete closed implementations drive registration, e.g. `typeof(IFooDefinition<>)`. |
| `As` | `Type?` | `null` | The facade service type each closed composition is registered as. Must be implemented by the composition class. |
| `Lifetime` | `InjectableLifetime` | `Singleton` | The lifetime each closed registration is given. |

`AllowMultiple = true`: a single composition type may carry several markers (e.g. exposing itself over multiple source interfaces or facades).

## Validation

Needlr provides compile-time analyzers to catch configuration errors:

| Diagnostic | Description |
|------------|-------------|
| [NDLRGEN035](analyzers/NDLRGEN035.md) | Source type must be an open generic interface |
| [NDLRGEN036](analyzers/NDLRGEN036.md) | Composition class must be an open generic with matching arity |
| [NDLRGEN037](analyzers/NDLRGEN037.md) | Composition class must specify and implement the `As` service type |
| [NDLRGEN038](analyzers/NDLRGEN038.md) | A discovered type argument violates the composition's constraints (registration skipped) |

## Namespace Import

The `[RegisterClosedOverImplementationsOf]` attribute is in the `NexusLabs.Needlr.Generators` namespace:

```csharp
using NexusLabs.Needlr.Generators;
```

This is intentional — it signals that the feature is source-generation-only.
