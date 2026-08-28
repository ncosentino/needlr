---
# AUTO-GENERATED from .github/instructions/genesis/time-provider.instructions.md — do not edit
paths:
  - "**/*.cs"
---
# TimeProvider

- Production code never reads `DateTime.Now`, `DateTime.UtcNow`,
  `DateTimeOffset.Now`, or `DateTimeOffset.UtcNow`.
- Forward an in-scope `TimeProvider` to timestamp, delay, timeout, timer, periodic
  timer, cancellation-timeout, and elapsed-time APIs. `NLF0025`/`NLF0026` enforce
  missed forwarding.
- Inject the BCL `TimeProvider` type. Do not create `ITimeProvider`, clock wrappers,
  or delay abstractions; `NLF0027` rejects them.
- Prefer `DateTimeOffset` for application values. Convert to `DateTime` only at an
  external schema/API boundary.
- Register `TimeProvider.System` once through the infrastructure-owning Needlr
  `IServiceCollectionPlugin`; do not register it ad hoc in `Program.cs`.
- Tests use `FakeTimeProvider`, seed time explicitly, and advance it only after the
  system under test has registered the timer it awaits.
