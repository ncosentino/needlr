---
# AUTO-GENERATED from .github/instructions/genesis/agentic/agent-observability.instructions.md — do not edit
paths:
  - "**/*Agent.cs"
  - "**/*AgentLoop.cs"
  - "**/*AgentWorkflow.cs"
  - "**/*Tool.cs"
  - "**/Agents/**/*.cs"
  - "**/Tools/**/*.cs"
  - "**/*Agent.ts"
  - "**/*AgentLoop.ts"
  - "**/*AgentWorkflow.ts"
  - "**/*Tool.ts"
  - "**/agents/**/*.ts"
  - "**/tools/**/*.ts"
  - "**/*.agent.ts"
  - "**/*.tool.ts"
---
# Agent Observability Rules

Apply these rules to telemetry for AI/LLM agents, model calls, model-driven
workflows, and model-invoked tools. A non-agent class that merely matches a generic
name such as `*Tool` follows ordinary telemetry guidance instead.

- Record the model identifier, latency, input/output token counts, cached and reasoning
  tokens when reported, iterations, tool calls, retries, and termination reason.
- Trace model calls, tool invocations, and other costly or failure-prone boundaries.
  Do not create a span for every pure prompt-construction helper.
- Keep operation names and metric dimensions bounded. Put run-specific identifiers only
  in allowlisted trace or log fields, never metric labels.
- Do not record full prompts, model responses, retrieved documents, tool payloads,
  secrets, credentials, or PII by default.
- Correlate model calls, tools, workflow stages, logs, and traces through supported
  context propagation rather than copying payloads between signals.
- Calculate monetary cost from versioned configuration or provider data; never hardcode
  pricing into instrumentation logic.
