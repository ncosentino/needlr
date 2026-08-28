---
# AUTO-GENERATED from .github/instructions/genesis/side-effect-idempotency.instructions.md — do not edit
paths:
  - "**/*Service.cs"
  - "**/*Repository.cs"
  - "**/*Worker.cs"
  - "**/*Job.cs"
  - "**/*Consumer.cs"
  - "**/*CarterModule.cs"
  - "**/*Client.cs"
  - "**/*Handler.cs"
  - "**/*UnitOfWork.cs"
  - "**/*Activity.cs"
  - "**/Tools/**/*.cs"
  - "**/*Service.ts"
  - "**/*Repository.ts"
  - "**/*Worker.ts"
  - "**/*Job.ts"
  - "**/*Consumer.ts"
  - "**/*Client.ts"
  - "**/*Handler.ts"
  - "**/*Activity.ts"
  - "**/*Tool.ts"
  - "**/tools/**/*.ts"
  - "**/*.activity.ts"
  - "**/*.tool.ts"
---
# Side-Effect Idempotency Rules

These rules apply to all retryable side effects. Agent/LLM-specific clauses apply
only when a model call or model-driven tool participates in the operation.

- Every retryable operation with an external side effect must use and document one
  strategy: naturally repeatable behavior, a provider idempotency key, or a durable
  deduplication record.
- Derive deduplication keys from stable workflow, request, and business identifiers.
  Never use the current time, randomness, or a newly generated ID.
- Reuse the identical provider idempotency key across every attempt of one logical
  operation.
- Do not rely on a non-atomic check-then-act sequence when concurrent attempts can
  both pass the check. Use a uniqueness constraint, transaction, or atomic ledger.
- Derive output paths and artifact names from stable inputs when retries should
  overwrite rather than duplicate.
- When an AI/LLM call is part of the operation, cache by stable input identity when
  reuse is valid or make every consuming side effect independently idempotent.
- A retry must not duplicate publishes, schedules, charges, notifications, messages,
  files, or externally visible state.
