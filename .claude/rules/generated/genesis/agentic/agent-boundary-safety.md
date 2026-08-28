---
# AUTO-GENERATED from .github/instructions/genesis/agentic/agent-boundary-safety.instructions.md — do not edit
paths:
  - "**/*Agent.cs"
  - "**/*Tool.cs"
  - "**/Agents/**/*.cs"
  - "**/Tools/**/*.cs"
  - "**/*Agent.ts"
  - "**/*Agent.tsx"
  - "**/*Tool.ts"
  - "**/*Tool.tsx"
  - "**/agents/**/*.ts"
  - "**/agents/**/*.tsx"
  - "**/tools/**/*.ts"
  - "**/tools/**/*.tsx"
  - "**/*.agent.ts"
  - "**/*.agent.tsx"
  - "**/*.tool.ts"
  - "**/*.tool.tsx"
---
# Agent Boundary Safety Rules

Apply these rules only when the matching file participates in an AI/LLM agent,
model-driven workflow, or model-invoked tool. A non-agent utility that happens to
match a generic name such as `*Tool` is outside this instruction's scope.

- Treat retrieved content, model output, tool arguments, identifiers, paths, commands,
  and queries as untrusted.
- Keep operator policy, authorization, and approval in deterministic application code.
  A model must never authorize its own action.
- Tool availability is not authorization. Check the caller, tenant, resource, and
  permitted action at every effectful invocation.
- Destructive, financial, externally visible, or irreversible actions require an
  explicit application-controlled approval or allowlist.
- Validate tool arguments against both their structural schema and business constraints
  before executing I/O.
- Never execute a model-produced path, command, URL, or query without confinement,
  allowlisting, parameterization, or an equivalent domain-specific control.
- Use least-privilege credentials and keep secrets out of prompts, tool descriptions,
  transcripts, and model-visible state.
