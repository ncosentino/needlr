---
applyTo: "**/*UnitOfWork.cs,**/*Service.cs,**/*Repository.cs,**/*Job.cs,**/*Consumer.cs,**/*CarterModule.cs,**/*Client.cs,**/*Handler.cs,**/*Worker.cs,**/*PostApi.cs"
---

# N+1 Call Prevention Rules

An N+1 occurs when code fetches a collection (1 query) and then makes an additional call per item
(N queries). This applies equally to database queries, MassTransit request/response calls, HTTP
requests, cache lookups, and any other I/O.

## How to spot it

```csharp
// ❌ N+1 — one call per item in a loop
var items = await _repository.GetAllAsync(ownerId, ct);
foreach (var item in items)
{
    var detail = await _client.GetDetailAsync(item.Id, ct);  // N calls
    results.Add(Combine(item, detail));
}
```

## What to do instead

**1. Use a bulk/batch API when one exists.**

```csharp
// ✅ Single bulk call replaces the loop
var items = await _repository.GetAllAsync(ownerId, ct);
var ids = items.Select(x => x.Id).Distinct().ToArray();
var details = await _client.GetDetailsByIdsAsync(ids, ct);  // 1 call
var detailsById = details.ToDictionary(d => d.Id);

foreach (var item in items)
{
    detailsById.TryGetValue(item.Id, out var detail);
    results.Add(Combine(item, detail));
}
```

**2. If no bulk API exists, create one.** Add a batch method to the relevant interface/repository/client
that accepts `IReadOnlyList<TId>` and returns results in a single round trip.

**3. For database repositories,** use `WHERE column IN @Ids` (Dapper) instead of looping with
single-row queries.

**4. For MassTransit cross-feature calls,** create a bulk contract that accepts an array of IDs
and a response that returns all results. This is one MassTransit round trip instead of N.

## Mandatory practices

- **Deduplicate IDs** before making the bulk call: `.Distinct().ToArray()`.
- **Use `TryGetValue`** on the result dictionary — never assume every ID will have a result.
  Missing keys must be handled gracefully (e.g., treated as "not found" or skipped).
- **Return only what callers need.** If the caller only needs two fields, create a slim projection
  DTO rather than returning full entity graphs with all fields and nested collections.

## When N+1 is acceptable

- The collection is **guaranteed to have at most 1–2 items** and no bulk API exists.
- The per-item call is a **local in-memory operation** with no I/O.

In all other cases, N+1 is a correctness and performance defect that must be fixed.

## Code review checklist

When reviewing or writing code in these file types, actively check for:
- `foreach` / `for` / LINQ `.Select()` loops that contain `await` calls to repositories, clients,
  or external services.
- Multiple sequential `await` calls that could be replaced by a single batch call.
- Task.WhenAll over per-item calls — this is **concurrent N+1**, not a fix. It still makes N calls;
  it just overlaps them. Use a genuine bulk API instead.
