---
applyTo: "AGENTS.md,CLAUDE.md,.github/copilot-instructions.md"
scope: "agent root entrypoints"
---

# Agent root files

These files load in every session and become project-owned after scaffold.

- Keep `AGENTS.md` at or below 60 lines and 3,072 UTF-8 bytes.
- Retain only project identity/routing and safeguards needed before any file is
  selected.
- Put architecture/rationale in project docs and exact technical rules in
  path-scoped instructions.
- Genesis-managed instructions are replaced by sync. Add project specialization in a
  separate instruction outside `.github/instructions/genesis/`.
- Propose changes to `AGENTS.md` explicitly; never rewrite a user's root guidance as an
  unrelated side effect.
- Keep `CLAUDE.md` as the one-line `@AGENTS.md` redirect.
- Keep `.github/copilot-instructions.md` as the minimal AGENTS pointer.
- Harness-specific content is the only reason to extend a redirect; do not duplicate
  general guidance there.
- Route delivery through `.github/skills/review-changes/SKILL.md` only while that
  seeded path exists.
