---
# AUTO-GENERATED from .github/instructions/genesis/web-dtos.instructions.md — do not edit
paths:
  - "**/*Request.cs"
  - "**/*Response.cs"
---
# Web API DTO Rules

## `long` is FORBIDDEN

The BGS Roslyn analyzer will block the build if `long` appears on a web API DTO. Every ID that is a `long` (or a `StronglyTypedId` backed by `long`) in the domain layer **must** be serialized as `string` in the request/response layer:

```csharp
// ❌ WRONG — will fail the build
public sealed record ThingResponse(long Id, string Name);

// ✅ CORRECT
public sealed record ThingResponse(string Id, string Name);
```

Use `Convert.ToString(id.Value, CultureInfo.InvariantCulture)` when mapping from domain → DTO.

## Three-layer separation

Never share types across these three layers:

| Layer | Type | Scope |
|-------|------|-------|
| Database | `private sealed record ThingDto` | Private to the repository |
| Domain | `public sealed record Thing(ThingId Id, ...)` | Immutable; passed between services and UnitOfWork |
| API | `public sealed record ThingResponse(string Id, ...)` | Carter module boundary only |

## Records

Prefer positional records for all three layers.

## Inline request/response types

Defining small request or response records directly inside a Carter module file is acceptable when the type is small and used exclusively by that module's endpoints.

## `*Response.cs` is web API only

`*Response` types are **exclusively** for Carter module (web API) boundaries. Do not use them for:

- **MassTransit consumer responses** — use `*ConsumerResponse.cs` (see the MassTransit contract instructions)
- **Programmatic service/job results** — use `*Result.cs` (see the result DTO instructions)

| Suffix | Boundary |
|--------|----------|
| `*Request` | Web API — incoming Carter module request body |
| `*Response` | Web API — outgoing Carter module response body |
| `*ConsumerResponse` | MassTransit — `context.RespondAsync(...)` payload |
| `*Result` | Domain/service — returned inside `TriedEx<T>` from services, jobs, or HTTP clients |
