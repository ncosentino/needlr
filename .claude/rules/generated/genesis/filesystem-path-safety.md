---
# AUTO-GENERATED from .github/instructions/genesis/filesystem-path-safety.instructions.md — do not edit
paths:
  - "**/*File*.cs"
  - "**/*Path*.cs"
  - "**/*Storage*.cs"
  - "**/*Tool.cs"
  - "**/*File*.ts"
  - "**/*Path*.ts"
  - "**/*Storage*.ts"
  - "**/*Tool.ts"
  - "**/*.tool.ts"
---
# Filesystem Path Safety Rules

These are general filesystem safety rules. References to models and model-invoked
tools apply only when an AI/LLM agent can supply or influence a path; ordinary
filesystem code still follows the non-agent confinement rules.

- Treat every caller-, API-, archive-, and configuration-supplied path as untrusted.
- When an AI/LLM agent is involved, treat every model-produced, model-selected, or
  retrieved-content-derived path as untrusted model output.
- Resolve paths through one sandbox-aware helper that canonicalizes the approved
  root and candidate, then proves the candidate remains inside that root.
- Do not validate confinement with string concatenation or a bare prefix comparison;
  account for separators, case rules, rooted paths, alternate path syntax, and
  sibling names that share a prefix.
- Reject absolute or parent-traversing inputs unless the contract explicitly allows
  them and validates them against an allowlist.
- Resolve or disallow symlinks and reparse points when they could escape the sandbox.
- Never interpolate an untrusted path into a shell command. Pass arguments through
  the process API's structured argument mechanism.
- Surface rejected paths as explicit validation or authorization failures; do not
  silently redirect them to another location.
