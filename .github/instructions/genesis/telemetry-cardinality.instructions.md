---
applyTo: "**/*.cs,**/*.ts,**/*.tsx"
---

# Telemetry Hygiene and Cardinality Rules

These rules apply to all application telemetry. References to prompts, model
responses, and token-bearing operations are additional safeguards only when an
AI/LLM agent or model integration is present.

- Instrument I/O, costly, slow, and failure-prone boundaries. Do not create spans for
  every pure in-memory helper.
- Use stable, low-cardinality operation and span names. Put request-specific values in
  allowlisted attributes only when operationally necessary.
- Metric labels and tags must come from bounded vocabularies. Never use raw request,
  user, tenant, entity, or correlation identifiers; URLs; paths; prompts; responses;
  exception messages; or free-form reasons as metric dimensions.
- Keep exported span attributes allowlisted. Do not record secrets, credentials, PII,
  full prompts, full model responses, or complete request/response payloads.
- Correlate logs and traces with supported trace/span context instead of duplicating
  narrative payloads across signals.
- Put precise diagnostic values in structured logs only when necessary and permitted
  by the project's redaction and retention policy.
