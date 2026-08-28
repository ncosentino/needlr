---
# AUTO-GENERATED from .github/instructions/genesis/n-plus-one-prevention.instructions.md — do not edit
paths:
  - "**/*UnitOfWork.cs"
  - "**/*Service.cs"
  - "**/*Repository.cs"
  - "**/*Job.cs"
  - "**/*Consumer.cs"
  - "**/*CarterModule.cs"
  - "**/*Client.cs"
  - "**/*Handler.cs"
  - "**/*Worker.cs"
---
# N+1 call prevention

- Do not place awaited repository, HTTP, message, cache, or other I/O calls inside
  per-item loops or LINQ projections.
- Deduplicate identifiers and call one bulk API. If no bulk API exists, add one to the
  owning interface/repository/client.
- Database batches use `WHERE ... IN @Ids`; cross-feature messaging uses a bulk
  request/response contract.
- Index bulk results once and handle missing keys with `TryGetValue`.
- `Task.WhenAll` over per-item I/O is concurrent N+1, not a fix.
- Per-item calls are acceptable only for guaranteed one-or-two-item collections or
  local in-memory work.
- Return the smallest projection the caller needs.
