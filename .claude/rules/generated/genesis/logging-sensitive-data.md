---
# AUTO-GENERATED from .github/instructions/genesis/logging-sensitive-data.instructions.md — do not edit
paths:
  - "**/*.cs"
  - "**/*.go"
  - "**/*.rs"
  - "**/*.ts"
  - "**/*.tsx"
  - "**/*.js"
  - "**/*.jsx"
  - "**/*.dart"
---
# What Is Safe To Log

This rule governs the *values* that reach a log sink. It applies to every language in the project
and composes with each stack's logging mechanism — it does not replace guidance on how log calls
are written.

Two distinct hazards share one remedy:

- **Disclosure** — a secret in a log is a secret in every downstream system that ingests logs:
  aggregators, dashboards, alerts, backups, and support tooling. Log retention usually outlives
  credential rotation.
- **Unbounded volume** — a value with no size ceiling turns a log line into an ingestion and
  storage cost, and buries the signal that made the line worth writing.

The remedy for both is the same: log bounded, allowlisted metadata.

## Never log these values

- Credentials — passwords, API keys, access and refresh tokens, `Authorization` header values,
  session cookies, private keys, connection strings
- Whole payloads — request and response bodies, query result sets, file contents, message bodies
- Query parameter values — bind them; do not interpolate them into a logged statement
- Model interaction content — prompts, completions, embeddings input, and tool call arguments or
  results

The last group is easy to miss because it rarely looks like a secret. Prompts and tool payloads
routinely carry user data, and their length is bounded only by a context window.

## Log identity and shape instead

Prefer values whose size is bounded by construction:

| Instead of | Log |
|------------|-----|
| The record | Its identifier |
| The result set | Its item count |
| The request body | Its byte length and content type |
| The failing input | Which validation rule failed |
| The token | Nothing — or the identifier of the principal it authenticated |
| The prompt | Model name, token counts, latency, finish reason |

Identifiers, counts, durations, status codes, and enum-valued outcomes are all safe by
construction. This is an allowlist: a value is loggable because you decided it was, not because
nothing obviously forbids it.

## Redaction is acceptable; truncation alone is not

Replacing a value with a fixed marker, or logging a salted hash when you need to correlate
occurrences without revealing the value, is fine. Truncating a secret is not — a prefix of a key
is still key material, and a prefix of a body is still unbounded in the dimension that matters.

## Diagnostic exceptions must be deliberate

Verbose payload logging is sometimes the only way to diagnose a protocol problem. When it is
genuinely required, it must be off by default, guarded by an explicit configuration switch, scoped
to a debug or trace level, and documented where the switch is defined. The default build must not
emit it.

Turning it on in production is a decision with a blast radius, not a convenience.
