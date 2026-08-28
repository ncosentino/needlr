---
# AUTO-GENERATED from .github/instructions/genesis/plugins.instructions.md — do not edit
paths:
  - "**/*Plugin.cs"
---
# Needlr plugins

- Needlr auto-registers ordinary concrete classes and implemented interfaces as
  singletons by default. Do not duplicate service/interface/collection/lazy
  registrations.
- `Configure()` contains only a lifetime/registration/framework primitive Needlr cannot
  infer or one deliberate startup side effect.
- Use `[Options]` and `[HttpClientOptions]` instead of manual options or named-client
  registration.
- Plugin implementations are already excluded from ordinary auto-registration; do not
  add `[DoNotAutoRegister]`.
- A plugin may perform one focused startup action that must run while building the
  provider, such as applying persisted culture or subscribing to process-wide events.
- If it has no manual concern and no startup side effect, delete it.
- Keep normal injectable behavior in services rather than plugin side effects.
