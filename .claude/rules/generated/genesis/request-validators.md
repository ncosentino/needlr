---
# AUTO-GENERATED from .github/instructions/genesis/request-validators.instructions.md — do not edit
paths:
  - "**/*RequestValidator.cs"
---
# Request validators

- Implement `AbstractValidator<TRequest>` beside the request type.
- Put structural/syntactic request rules in the validator, not the Carter handler or
  unit of work.
- Non-obvious failures end with a message that states what is invalid and how to fix
  it.
- Use `When` for conditional rules and `Must` for custom predicates; do not duplicate
  the same rule across validators/operations.
- Carter handlers inject `IValidator<TRequest>`, call `ValidateAsync` with the request
  cancellation token, use `ConfigureAwait(false)`, and return
  `validationResult.ToResult()` when invalid.
- Do not use `ThrowIfInvalidAsync`; validation failure is expected control flow.
- Needlr discovers validators automatically; do not register them manually.
