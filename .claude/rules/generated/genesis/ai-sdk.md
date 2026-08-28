---
# AUTO-GENERATED from .github/instructions/genesis/ai-sdk.instructions.md — do not edit
paths:
  - "**/src/server/ai/**/*.{ts,tsx}"
  - "**/app/api/ai/**/*.{ts,tsx}"
  - "**/src/components/ai/**/*.{ts,tsx}"
---
# AI SDK boundaries

- Keep application services typed against AI SDK `LanguageModel` or a provider-neutral model
  resolver. Provider adapters must not leak into Route Handler, tool, approval, or UI contracts.
- Validate structured model output and tool arguments at runtime with schemas.
- Return explicit expected-failure results for configuration, provider, validation, and
  cancellation outcomes. Do not expose raw provider errors to clients.
- Pass request `AbortSignal` values through streaming, generation, and tool execution.
- Tool availability is not authorization. Tools that represent outward or destructive actions
  should produce proposals; deterministic application policy and human approval decide whether
  side effects may execute.
- Record model/provider ids, tool names, token counts, finish reason, and termination state.
  Never record prompt bodies, generated content, tool arguments/results, or credentials by
  default.
- Unit tests use deterministic AI SDK mock models. Live-provider tests are opt-in and must not
  join routine local or merge-gate validation.
- Document every exported type, function, provider, tool, and result contract with JSDoc.
