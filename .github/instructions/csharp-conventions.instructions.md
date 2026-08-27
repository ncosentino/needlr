---
applyTo: "**/*.cs"
---

# Needlr C# Conventions

## File layout

- **One type per file.** Never put multiple classes, structs, records, or enums in the same `.cs` file.
- **File-scoped namespaces** everywhere — `namespace Foo;`, not `namespace Foo { }`.
- **`internal` by default.** Only types a consumer references directly should be `public`.

## Type selection

- **Data carriers are records.** DTOs, options, contexts, definitions, results, snapshots,
  evidence, counts, and structured failures must be `record` types, including body-style
  records with validated constructors. Reserve classes for behavior, mutable runtime
  state, and services.
- **Non-static class-only non-services require `[DoNotAutoRegister]`.** Never rely on
  `required` members, constructor shape, generic arity, visibility, or namespace filters
  to keep an instantiable class out of Needlr's automatic DI registration.

## Naming

- PascalCase for all public members, types, and namespaces.
- `_camelCase` for private fields.
- Diagnostic IDs use the `NDLR` prefix plus a component code: `NDLRCOR` (core),
  `NDLRGEN` (generators), `NDLRSIG` (SignalR), `NDLRLOG` (logging), `NDLRHTTP` (HttpClient).

## XML documentation

Required on all `public` types and members, using `<summary>`, `<param>`, `<returns>`, and
`<example>` where appropriate. Internal types need XML docs on the type itself; individual
members are optional unless non-obvious.

## Design principles

- **Composition over inheritance.** Base classes should be extremely rare and exist only
  as pure convenience for implementors. Always prefer interfaces plus composition. If a
  pattern has common boilerplate, solve it with source generation or composable helper
  types, not an inheritance hierarchy.
- **Interfaces over static classes.** Static classes are acceptable only for trivial value
  calculations or extension-method containers. Anything with behavior, state, or
  dependencies must be an interface registered through DI.
- **No static singleton holders.** Never use a `static Instance` property, a `static Holder`
  class, or any pattern that stores a singleton in a static field to share state between
  components. It destroys testability, breaks multi-threaded scenarios, and is antithetical
  to dependency injection. Pass dependencies through constructors, method parameters, or
  DI — never through static state. This is a dependency injection library; use dependency
  injection.
