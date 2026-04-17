---
# AUTO-GENERATED from .github/instructions/genesis/unit-of-work.instructions.md — do not edit
paths:
  - "**/*UnitOfWork.cs"
---
# Unit of Work Rules

A UnitOfWork encapsulates a single, named business operation with potentially multiple steps.

## Always pair with an interface

Every UnitOfWork class MUST have a corresponding `IXxxUnitOfWork` interface defined in a separate file. Carter modules and consumers inject the interface, not the concrete class:

```csharp
// ICreateThingUnitOfWork.cs
public interface ICreateThingUnitOfWork
{
    Task<TriedEx<ThingId>> TryCreateAsync(
        UserId ownerUserId,
        string name,
        CancellationToken cancellationToken);
}

// CreateThingUnitOfWork.cs
internal sealed class CreateThingUnitOfWork(
    ILogger<CreateThingUnitOfWork> _logger,
    IIdGenerator _idGenerator,
    ThingRepository _thingRepository) :
    ICreateThingUnitOfWork
{
    public async Task<TriedEx<ThingId>> TryCreateAsync(
        UserId ownerUserId,
        string name,
        CancellationToken cancellationToken) => await
    Try.GetAsync<ThingId>(_logger, async () =>
    {
        // business logic here
    });
}
```

## Return types

- Methods named `TryXxxAsync`, always returning `TriedEx<T>` or `Exception?`
- NEVER throw — NEVER return void
- Wrap the method body in `Try.GetAsync<T>(_logger, async () => { ... })`

## Visibility

- The interface may be `public` (if consumed by other feature assemblies) or `internal`
- The implementation is always `internal sealed class`

## Scope of responsibility

- Coordinates repositories — may call multiple repos within a single operation
- Applies business rules and validations between repository calls
- May publish domain events (via `IEndpointPublisherProvider`)
- Has **NO knowledge** of HTTP, Carter modules, or API request/response types

## FORBIDDEN in UnitOfWork

### Web API request DTOs as parameters

NEVER accept a web request DTO (e.g., `CreateSiteRequest`, `UpdateThingRequest`) as a UnitOfWork parameter. These types are bound at the HTTP layer and must not bleed into business logic. The Carter module owns the translation from the web DTO to a service-layer input type:

```csharp
// WRONG — request DTO bleeds into business logic
Task<TriedEx<Site>> TryCreateAsync(CreateSiteRequest request, string ownerUserId, CancellationToken cancellationToken);

// CORRECT — UoW accepts a service-layer input type
Task<TriedEx<Site>> TryCreateAsync(CreateSiteInput input, string ownerUserId, CancellationToken cancellationToken);
```

Define a dedicated `*Input` record in the same feature slice as a service-layer type. The Carter module maps the web DTO into this type before calling the UoW.

### Suppressing analyzer rules

`[System.Diagnostics.CodeAnalysis.SuppressMessage]` is **STRICTLY FORBIDDEN** without explicit team lead approval. Never suppress analyzer warnings to make code compile. Fix the code instead.

### LogError for runtime failures

Use `LogWarning` — not `LogError` — when logging transient or expected runtime failures (e.g., a database commit that failed, a record not found, a conflict). `LogError` is reserved for systematic failures that indicate the service itself is broken. Runtime errors that can happen for any transient reason are warnings:

```csharp
// WRONG
error => _logger.LogError(error, "Failed to commit {SiteId}", id)

// CORRECT
error => _logger.LogWarning(error, "Failed to commit {SiteId}", id)
```

## Testing

Tests target UnitOfWork classes directly with real MySQL via `TestFixtureBuilder`. Do NOT test business logic through Carter modules or consumers — test the UnitOfWork directly.

## Reference

- `documentation/architecture/patterns/try-patterns.md`
