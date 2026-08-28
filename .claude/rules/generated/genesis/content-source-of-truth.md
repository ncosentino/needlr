---
# AUTO-GENERATED from .github/instructions/genesis/content-source-of-truth.instructions.md — do not edit
paths:
  - "**/*.md"
  - "**/*.mdx"
---
# Canonical Content Ownership

Apply these rules when the repository designates a maintained page or source as the
canonical owner of a concept. They do not make ADRs, changelogs, migration guides,
incident records, instruction files, or intentionally self-contained documentation
link-only.

- Keep the complete maintained explanation in its canonical owner.
- Other prose should include the local context readers need and link to the canonical
  detail instead of copying substantial content that would require synchronized edits.
- Never replace necessary meaning with a bare link; summarize why the canonical source
  is relevant.
- When preserving a historical or offline snapshot is intentional, identify its source
  and version so readers do not mistake it for the live canonical definition.
