---
# AUTO-GENERATED from .github/instructions/genesis/async-hygiene.instructions.md — do not edit
paths:
  - "**/*.cs"
---
# Async hygiene

- Keep asynchronous call chains asynchronous. Use `await` rather than blocking on a `Task` with
  `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()`.
- Do not wrap a synchronous-only I/O or storage API in `Task.Run` merely to expose an async-shaped
  API. Prefer genuine asynchronous APIs; when none exists, keep the blocking boundary explicit
  instead of claiming non-blocking I/O.
- `Task.Run` remains appropriate for CPU-bound work or deliberate outer-boundary offloading such as
  protecting a UI thread, but it still consumes a thread-pool thread and concurrency must remain
  bounded.
- Use `Channel.CreateBounded<T>` with an explicit capacity when a channel provides queueing or
  backpressure. If a synchronous producer must feed an asynchronous consumer, use a synchronous
  backpressure boundary rather than blocking on an asynchronous operation.
