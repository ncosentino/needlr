---
# AUTO-GENERATED from .github/instructions/genesis/needlr-factories.instructions.md — do not edit
paths:
  - "**/*.cs"
---
# Needlr factories

- When construction combines DI services with per-call values, generate a factory;
  do not manually construct the type or hand-write the factory.
- Use `[GenerateFactory<TInterface>]` and `[GenerateConstructor]` on a `partial`
  implementation with `private readonly` fields.
- Plain `[GenerateFactory]` returns the concrete type, so that type must be public.
- `Create(...)` accepts runtime values by field name; services remain injected.
- Inject the generated factory, not the decorated type. The type itself is not
  registered.
- Import the type's `.Generated` namespace for the factory interface.
- `[DoNotAutoRegister]` suppresses registration only; it does not generate a factory.
