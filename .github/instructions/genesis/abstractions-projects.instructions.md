---
applyTo: "**/*.Abstractions/**,**/*.Abstractions.csproj"
---

# Abstractions Project Rules

An `*.Abstractions` project publishes a contract — interfaces and the types those interfaces speak — that downstream consumers can reference WITHOUT taking on the third-party dependency that some other project will use to implement them.

The whole value of an abstractions project is that it carries **zero coupling** to the third-party world. Everything in this file exists to protect that.

## Naming and placement

- The project is named `<Owner>.Abstractions`, where `<Owner>` is the project that publishes the contract.
- Examples: `MyProduct.Kernel.Caching.Abstractions`, `MyProduct.Features.Media.Abstractions`.
- The project folder is a **sibling** of the owner project. Do NOT nest it inside the owner.

## Allowed contents

The abstractions project may contain:

- Interfaces that describe the contract (e.g. `ICacheProvider`, `IImageResizer`).
- DTOs and records that those interfaces accept or return.
- Options classes with the Needlr `[Options]` attribute.
- Domain exceptions raised by adapters and surfaced to consumers.
- Enums and constants that belong to the contract.

That is the entire allow-list.

## Forbidden contents

Nothing in this project may:

- Reference the third-party package(s) that adapters use to implement the contract. A third-party reference here defeats the project's reason to exist — every consumer of the contract would transitively pull the vendor in.
- Register services with MS.DI. No `IServiceCollectionPlugin`, no `IPostBuildServiceCollectionPlugin`, no `IWebApplicationPlugin`, no extension methods that call `AddSingleton<>` / `AddScoped<>` / `AddTransient<>`.
- Contain runtime behavior. No static configurators, no default implementations, no "no-op" fallback plugins. If you want a `NoOpFooFallbackPlugin` co-located with the contract because "it's only one tiny class", put it in the owner project instead. The line erodes the moment we allow one exception.
- Expose any public surface whose signature mentions a third-party type.

## Allowed dependencies

- `NexusLabs.Framework` and similar contract-only utility packages.
- Needlr attribute-only packages (so `[Options]` and similar attributes resolve at compile time).
- `System.*` BCL only.

If a package brings in transitive third-party-of-concern dependencies, it does not belong here.

## Common pitfalls

- **Adding a logger or telemetry package.** Logging is observable runtime behavior; the abstraction is a contract. Loggers belong in the owner or the adapter.
- **Letting a third-party type leak through a returned object.** The contract returns your own DTOs, not the vendor's. If you return a third-party type you have coupled every consumer of the contract.
- **Adding an extension method that takes `IServiceCollection`.** Even if the body is empty, having it here invites the next contributor to fill it in with a registration.
- **Splitting "just because there's a third-party dep."** If none of the split-criteria apply, take the dep directly in the owner instead of spinning up an empty Abstractions project.
