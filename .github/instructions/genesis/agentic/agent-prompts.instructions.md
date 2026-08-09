---
applyTo: "**/*Prompt*.cs,**/Prompts/**/*.cs,**/*Prompt*.ts,**/*Prompt*.tsx,**/prompts/**/*.ts,**/prompts/**/*.tsx,**/*.prompt.ts,**/*.prompt.tsx"
---

# Agent Prompt Rules

Apply these rules when a matching prompt is sent to or consumed by an AI/LLM
agent. UI copy, CLI questions, logging templates, and other non-model prompts are
outside this instruction's scope.

- Put trusted operator policy in the framework's instruction or system channel. Treat
  retrieved, user, tool, and model-produced text as data, never as trusted policy.
- Delimit and label untrusted context so it cannot be mistaken for an instruction.
- Keep reusable stable context before volatile per-request content. Preserve
  deterministic ordering and serialization when prompt caching or reproducibility
  matters.
- Do not interpolate timestamps, request IDs, randomness, or other volatile values into
  a shared stable prefix.
- Prefer provider-supported structured output when available, but always validate the
  returned runtime shape and handle refusals or missing structured content.
- Never place secrets, credentials, private keys, or unnecessary sensitive data in a
  prompt.
- Verify current provider documentation for model IDs, cache limits, TTLs, pricing,
  structured-output constraints, and other version-sensitive behavior.
