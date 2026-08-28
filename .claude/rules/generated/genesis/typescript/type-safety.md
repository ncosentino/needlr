---
# AUTO-GENERATED from .github/instructions/genesis/typescript/type-safety.instructions.md — do not edit
paths:
  - "**/*.ts"
  - "**/*.tsx"
---
# TypeScript Boundary Safety Rules

These are general TypeScript boundary rules. References to model output and
model-invoked tools apply only when an AI/LLM agent or model integration is present.

- Values crossing HTTP, storage, environment, deserialization, or other untyped
  boundaries begin as `unknown` and must be validated or narrowed before use.
- When an AI/LLM agent is involved, model output, retrieved content, and
  model-produced tool arguments are untrusted external boundary data.
- Do not use `any`, object-literal assertions, chained assertions, or non-null
  assertions to bypass boundary validation.
- Parse JSON as untrusted data. When an AI/LLM agent is involved, apply the same rule
  to structured model output. Validate the runtime shape with the project's schema or
  type-guard mechanism.
- Keep type assertions confined to boundaries where TypeScript cannot represent a
  proven runtime fact. Document the non-obvious reason rather than normalizing casts
  throughout business logic.
- Prefer readonly public contracts and readonly collection views when callers should
  not mutate shared data.
- Do not rewrite generated or vendored code solely to enforce project-owned type
  preferences.
