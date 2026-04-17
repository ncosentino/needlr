---
# AUTO-GENERATED from .github/instructions/genesis/repositories.instructions.md — do not edit
paths:
  - "**/*Repository.cs"
---
# Repository Pattern Rules

## Injection

- Always inject `IDbConnectionFactory` — never a raw `IDbConnection`
- Inject `ILogger<TRepository>` for structured logging
- Inject a `*CacheProvider` (wrapping `IFusionCache`) if reads benefit from caching

## Performance: minimize round trips

Database round trips are **the single most expensive operation** in the application. Every query
crosses a network boundary with serialization, deserialization, connection pool contention, and
latency overhead. Treat each round trip as costly I/O that must be justified.

**Mandatory rules:**

1. **One method = one query whenever possible.** If a repository method needs data from two queries,
   combine them into a single query (JOINs, CTEs, subqueries, multi-result sets via `QueryMultipleAsync`).
   Two sequential queries in the same method is a code smell that requires explicit justification.

2. **Never resolve IDs in one query and then fetch data in a second query.** If you need to look up
   an entity by an alternate key (e.g., resolve a revision ID to a content ID, then fetch fields),
   do it in a single query with a JOIN or subquery — not two round trips.

3. **Batch operations use `WHERE column IN @Ids`** — never loop with single-row queries.

4. **Cache aggressively.** If a read is called more than once for the same key in a request lifetime
   or across requests, it must use the injected `*CacheProvider` via `GetOrSetAsync`. See the Caching
   section below for the required pattern.

5. **Return only what callers need.** Use projections and slim DTOs — never `SELECT *` or full entity
   graphs when the caller only needs a subset of fields.

**If you find yourself writing two `await connection.QueryAsync` calls in the same method, stop and
reconsider your query design.** The correct solution is almost always a single query that does both
lookups in one round trip.

## Return types

Choose the return type that accurately expresses intent:

| Intent | Return type |
|--------|-------------|
| Value or error (value is never null) | `TriedEx<T>` |
| Value, null, or error | `TriedNullEx<T?>` |
| Success or failure (no return value) | `Exception?` |

## Producing Try results

Repository methods returning `TriedEx<T>`, `TriedNullEx<T?>`, or `Exception?` **MUST** use the corresponding
`Try` helper — never a hand-rolled try/catch:

```csharp
// CORRECT — Try.GetAsync wraps exceptions, handles logging and telemetry
public async Task<TriedEx<Thing>> TryGetByIdAsync(
    ThingId id,
    CancellationToken cancellationToken) => await
Try.GetAsync<Thing>(_logger, async () =>
{
    // ... Dapper query, mapping, return new TriedEx<Thing>(value)
});

// CORRECT — synchronous variant
public TriedEx<IReadOnlyList<T>> TryDeserialize<T>(string? json) =>
    Try.Get<IReadOnlyList<T>>(() =>
    {
        // ... deserialization logic, return new TriedEx<IReadOnlyList<T>>(value)
    });
```

```csharp
// WRONG — hand-rolled try/catch bypasses logging and telemetry
public async Task<TriedEx<Thing>> TryGetByIdAsync(...)
{
    try { ... return new TriedEx<Thing>(value); }
    catch (Exception ex) { return new TriedEx<Thing>(ex); }
}
```

The `Try` wrappers provide systematic error logging and telemetry that manual catch blocks bypass entirely.

## Data mapping

Map DB rows to a `private sealed record` DTO first. Always convert to an immutable domain object before returning to callers. Never expose DB DTOs outside the repository class.

## CancellationToken

`CancellationToken cancellationToken = default` is **FORBIDDEN**. `CancellationToken` is always a required, non-optional parameter. This applies to every repository method, interface method, UnitOfWork method, and service method — no exceptions.

## Method signatures

Every method with more than one parameter **must** put each parameter on its own line. This makes diffs readable and keeps signatures consistent:

```csharp
// CORRECT
public async Task<Site?> GetSiteBySlugAsync(
    string ownerUserId,
    string siteSlug,
    CancellationToken cancellationToken) => await
Tracer.Default.WithTracingAsync(async () =>
{

// WRONG — do not inline multiple parameters
public async Task<Site?> GetSiteBySlugAsync(string ownerUserId, string siteSlug, CancellationToken cancellationToken) => await
```

This rule applies everywhere: repository methods, interface declarations, UoW methods, carter module route handlers, and service methods.

## Tracing

Wrap **every** public method in `Tracer.Default.WithTracingAsync` using the **expression-body form** — `=> await` ends the method signature line, `Tracer.Default` continues at the same indentation level as the method keyword:

```csharp
public async Task<Persona?> GetByIdAsync(
    PersonaId id,
    CancellationToken cancellationToken) => await
Tracer.Default.WithTracingAsync(async () =>
{
    // query here
});
```

**Never** use the block-body form — it creates an unreadable double-brace nesting:

```csharp
// WRONG — never do this
public async Task<Persona?> GetByIdAsync(PersonaId id, CancellationToken cancellationToken)
{
    return await Tracer.Default.WithTracingAsync(async () =>
    {
        // query here
    });
}
```

## Write methods: two-overload pattern

For any write that may be called inside an external transaction (e.g., from a UnitOfWork using `TryCommitAndRollbackOnFailAsync`), expose **two public overloads**:

1. **Transaction overload** — receives the caller's `IDbTransaction`; contains the actual SQL. Gets its own tracing span.
2. **No-transaction overload** — opens a connection, begins its own transaction, calls the transaction overload, then commits. Also gets its own tracing span.

```csharp
// Transaction overload: executes SQL using the caller's transaction
public async Task CreateThingAsync(IDbTransaction transaction, Thing thing, CancellationToken cancellationToken) => await
    Tracer.Default.WithTracingAsync(async () =>
    {
        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            Sql,
            param,
            transaction: transaction,
            cancellationToken: cancellationToken));

        _logger.LogInformation("Created thing {ThingId}", thing.Id);
    });

// No-transaction overload: opens its own connection, wraps in transaction, delegates
public async Task CreateThingAsync(Thing thing, CancellationToken cancellationToken) => await
    Tracer.Default.WithTracingAsync(async () =>
    {
        using var connection = await _dbConnectionFactory.OpenNewConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await CreateThingAsync(transaction, thing, cancellationToken);
        transaction.Commit();
    });
```

**Never** extract SQL into a private `Execute*` method shared between the two overloads. A private method cannot be traced individually, and the no-transaction overload loses its span visibility when it delegates to a private implementation.

## Caching

For reads that benefit from caching, use `GetOrSetAsync` via the injected cache provider:

```csharp
var result = await _cacheProvider.Cache
    .GetOrSetAsync<IReadOnlyList<MyType>>(
        cacheKey,
        async (ctx, ct) =>
        {
            // execute SQL here and return result
        },
        token: cancellationToken);
```

Always invalidate the relevant cache key(s) after any write operation.

### ⛔ FORBIDDEN: `TryGetAsync` + `SetAsync` manual pattern

**Never** use the manual `TryGetAsync` + `SetAsync` pattern. It bypasses FusionCache's built-in
stampede protection (cache stampede / thundering herd), which means multiple concurrent cache
misses can all hit the database simultaneously:

```csharp
// ❌ WRONG — bypasses stampede protection, do NOT use
var cached = await _cacheProvider.Cache.TryGetAsync<T>(cacheKey, token: cancellationToken);
if (!cached.HasValue)
{
    var fromDb = await FetchFromDbAsync(...);
    await _cacheProvider.Cache.SetAsync<T>(cacheKey, fromDb, options, token: cancellationToken);
    return fromDb;
}
return cached.Value;
```

`GetOrSetAsync` handles cache misses atomically with a factory delegate, ensuring only one caller
hits the database per cache miss. Always use it.

## Purity

- Pure data access only — no business logic, no branching on domain rules
- NEVER produce side effects on tables owned by another feature domain
- `social_account_auth` is owned by `NexusAI.Features.Authentication` — do not write to it from any other feature's repository; inject `SocialAuthRepository` instead

## Cross-domain JOINs

Each domain repository may maintain its own cache. A query that JOINs two domain tables bypasses both caches and may return stale data. This is acceptable for background jobs or batch reads that need fresh data — but avoid it on hot user-facing read paths. Call each domain's repository separately so caches are respected. Document the trade-off explicitly when writing a cross-domain query.

## Reference

- `documentation/architecture/patterns/repository-pattern.md`
- `documentation/architecture/patterns/try-patterns.md`
