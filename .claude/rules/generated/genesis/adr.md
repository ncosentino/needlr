---
# AUTO-GENERATED from .github/instructions/genesis/adr.instructions.md — do not edit
paths:
  - "**/adr-*.md"
  - "**/ADR-*.md"
  - "**/*-adr-*.md"
  - "**/*-ADR-*.md"
  - "**/*-adr.md"
  - "**/*-ADR.md"
---
# Architecture decision records

- Record one significant decision that changes structure, technology, integration
  boundaries, quality attributes, or another costly-to-reverse convention.
- Default to `docs/adr/adr-NNNN-title-slug.md` with sequential four-digit numbering.
- Use frontmatter fields `title`, `status`, `date`, `authors`, `tags`, `supersedes`,
  and `superseded_by`.
- Status is `Proposed`, `Accepted`, `Rejected`, `Superseded`, or `Deprecated`.
- Include context/scope, verified facts versus assumptions, decision drivers, the
  explicit decision, serious alternatives, consequences, confirmation, and references.
- Explain what cited code/config/evidence demonstrates; a path or issue is not
  self-explanatory rationale.
- Proposed records may change during review. Do not rewrite an accepted decision;
  create/link a superseding ADR for material change.
- Keep decision meaning in the ADR and implementation chronology in issues/PRs.
- Check existing ADRs before creating one and maintain both sides of supersession links.
- Do not turn an ADR into a plan, changelog, design guide, or conversation transcript.
