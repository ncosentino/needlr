---
applyTo: "**/*.cs"
---

# Structured logging

- New production log calls use `[LoggerMessage]`; do not add direct `LogXxx` calls,
  interpolation, or concatenated messages. Migrate existing direct calls when touched.
- Put up to three generated log methods on the consuming `partial` class. At four or
  more, use one `{Feature}Log` partial class in the feature namespace.
- Message templates use stable named properties. Never log secrets, tokens, connection
  strings, raw request/response bodies, or unbounded user-controlled values.
- Open one `BeginScope` at request/message/job/unit-of-work entrypoints for stable
  identifiers. Do not create nested scopes throughout downstream code.
- Use `Debug` for diagnostics, `Information` for business events, `Warning` for
  expected/transient failures, `Error` for systematic service failure, and `Critical`
  only when the process cannot continue.
- Do not emit one log per item on an unbounded collection or one success log on every
  hot-path call. Aggregate or sample when cardinality can grow with input.
- Tests that do not assert logging use `NullLogger<T>.Instance`, not a logger mock.
