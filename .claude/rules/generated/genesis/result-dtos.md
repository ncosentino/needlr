---
# AUTO-GENERATED from .github/instructions/genesis/result-dtos.instructions.md — do not edit
paths:
  - "**/*Result.cs"
---
# Result DTO Rules

`*Result` types are **programmatic result DTOs** — they carry the data returned by a service, job, or HTTP client method. They are domain-layer types, not web-layer types.

## What `*Result` is for

```
Service / Client / UnitOfWork
    └─ returns TriedEx<MyOperationResult>
           └─ MyOperationResult carries the data (success path only)
```

- A `*Result` type lives inside a `TriedEx<T>` or `TriedNullEx<T?>` wrapper
- The wrapper owns success/failure signalling — the `*Result` type itself never carries `Success`, `Error`, or `IsValid` properties
- Used for jobs, HTTP clients, schedulers, and services that need to return structured data to a caller

## What `*Result` is NOT

- Not a web API response — use `*Response.cs` for Carter module boundaries
- Not a MassTransit response — use `*ConsumerResponse.cs` for `context.RespondAsync()`
- Not a domain entity — use a plain domain record (e.g., `ScheduledPost`, `Blog`) for persisted objects

## Shape

```csharp
// ✅ CORRECT — immutable positional record, carries data only
public sealed record OneShotJobScheduleResult(
    JobExecutionId JobExecutionId,
    string JobKey);

// ✅ CORRECT — returned inside a Try wrapper
public async Task<TriedEx<OneShotJobScheduleResult>> TryScheduleJobAsync(...) => await
    Try.GetAsync<OneShotJobScheduleResult>(_logger, async () =>
    {
        // ...
        return new OneShotJobScheduleResult(executionId, jobKey);
    });

// ❌ WRONG — result types do not own success/failure
public sealed record MyResult(bool Success, string? Error, string? Value);
```

## Rules

- **Immutable positional record** — always
- **`long` is allowed** — these are domain types, not web types; strong-typed IDs are preferred but `long` is not banned here
- **One type per file** — every `*Result.cs` file contains exactly one type; the filename matches the type name
- **Scope** — `public` if the result crosses project boundaries (e.g., SDK); `internal sealed` if used only within the feature project

## Placement

- If the result type is shared between the SDK and a consumer/feature, it lives in the **SDK project**
- If it is used only within one feature, it lives in the **feature's vertical slice folder** alongside the class that produces it
