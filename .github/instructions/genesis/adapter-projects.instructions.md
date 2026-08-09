---
applyTo: "**/*.Adapters.*/**,**/*.Adapters.*.csproj"
---

# Adapter Project Rules

An `*.Adapters.<Vendor>` project is the **only place** in the codebase allowed to type the name of the third-party package it adapts. Its job is to implement one or more abstractions using exactly one vendor's library, and to register those bindings via an internal Needlr plugin so the composition root can wire it by ProjectReference.

## Naming and placement

- The project is named `<RootNamespace>.Adapters.<Vendor>`. **Plural** `Adapters`. There is **no** `<Capability>` slot between `Adapters` and `<Vendor>` — the project name names the vendor and nothing else.
- The project folder lives at the top level under `src/Adapters/`, **parallel** to `src/Features/`, `src/Kernel/`, and `src/Applications/`. Adapters are a top-level concept; they do NOT live nested under the owner they implement against.

Examples:

```
✅ MyProduct.Adapters.FusionCache
✅ MyProduct.Adapters.AzureOpenAI
✅ MyProduct.Adapters.NetVips

❌ MyProduct.Adapters.Caching.FusionCache    ← no <Capability> slot allowed
❌ MyProduct.Kernel.Caching.FusionCache      ← missing "Adapters" marker
❌ MyProduct.Features.Media.Adapters.NetVips ← do not nest under the owner
```

## One vendor per project

Each adapter project adapts exactly one third-party library. Do NOT bundle multiple vendors into one project — that defeats the swap-out lever the pattern exists to provide.

## Multiple abstractions per project is fine

When a single vendor naturally covers several lightweight abstractions, implement them all from one adapter project. For example, an `Adapters.AzureOpenAI` project can implement `IChatClient` from `Features.Chat.Abstractions` AND `IEmbeddingsClient` from `Features.Embeddings.Abstractions`. One vendor, multiple contracts — fine.

## References

An adapter project's `.csproj` references:

- The third-party package(s) being adapted.
- One or more `*.Abstractions` `ProjectReference` entries whose interfaces this adapter implements.
- Needlr DI infrastructure packages required to expose a plugin.

An adapter project **MUST NOT** reference its owner project (e.g. `Features.Media`, `Kernel.Caching`). That creates a cycle back through the abstraction and breaks the whole point of the split.

## Plugin registration

The adapter exposes its DI bindings via an `internal` Needlr plugin (`IServiceCollectionPlugin` / `IPostBuildServiceCollectionPlugin` / `IWebApplicationPlugin` as appropriate). The plugin is `internal` because the **composition root or its dependency-bootstrapping aggregator** (e.g. a `*.Bootstrap` project) selects the adapter by ProjectReference, not by importing the plugin type. There is no scenario in which a downstream project should `using` the plugin namespace to register the adapter — that would defeat the inversion-of-control seam this project shape exists to create.

## `Missing*` fallback note

If the contract needs a `Missing*` fallback implementation — one that throws with an actionable setup-error message when no real adapter is wired — it does **NOT** live here. It lives in the **owner** project that publishes the abstraction.

## Common pitfalls

- **Referencing an owner project from inside the adapter.** Creates a cycle. The adapter sees only the abstraction; the owner sees only the abstraction.
- **Adding a second vendor's package** "just for a little helper". One vendor per project. If you need another vendor, that is another adapter project.
- **Leaking the third-party type through a public surface.** No public method on your implementation should return a vendor-specific type. The implementation lives on the contract's terms; downstream consumers must never have to type the vendor name.
- **Putting the `Missing*` fallback here.** Wrong place. It belongs in the owner project.
- **Calling `services.AddSingleton<>` for types Needlr already auto-registers.** The adapter plugin should register only what Needlr cannot auto-discover — specific lifetimes, factory methods, decorators, vendor-options binding. Plain class registrations are noise that duplicates the auto-discovery and breaks lifetime expectations.
