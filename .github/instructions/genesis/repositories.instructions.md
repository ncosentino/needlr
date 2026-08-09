---
applyTo: "**/*Repository.cs"
---

# Repository contracts

- Inject `IDbConnectionFactory`, never a raw connection. Inject `ILogger<T>` and the
  owning cache provider when reads are cached.
- Minimize round trips: one method uses one query when practical; use joins/CTEs,
  multi-result queries, and `WHERE ... IN @Ids` instead of lookup-then-fetch or loops.
- Project only required fields; never `SELECT *` or expose database DTOs.
- Map rows through private sealed DTOs and return immutable domain values.
- Return `TriedEx<T>` for value/error, `TriedNullEx<T?>` for value/null/error, and
  `Exception?` for success/failure without a value.
- Produce result contracts through `Try.Get`/`Try.GetAsync`; do not hand-roll catch
  blocks that bypass logging/telemetry.
- `CancellationToken` is required and never defaults. Multi-parameter signatures put
  one parameter per line.
- Wrap each public method in expression-bodied
  `Tracer.Default.WithTracingAsync`; do not add block-body wrapper nesting.
- Writes callable inside an external transaction expose a transaction overload that
  owns SQL plus a no-transaction overload that opens/commits and delegates. Both are
  traced; do not hide SQL in an untraced private executor.
- Cached reads use `GetOrSetAsync`, never manual `TryGetAsync` then `SetAsync`.
  Invalidate affected keys after writes.
- Repository code is pure data access. It does not enforce business rules or write
  another feature's tables.
- Avoid cross-domain joins on hot cached paths. When batch/background work requires
  one, document the freshness/cache tradeoff.
- SQLite UUID repositories also follow the dedicated codec rules.
