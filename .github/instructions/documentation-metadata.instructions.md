---
applyTo: "src/**/*.cs,docs/**/*.md,docs/hooks/**/*.py,overrides/**,mkdocs.yml,scripts/test-docs-titles.py,scripts/refresh-docs-titles.py"
---

# Documentation Metadata

- API descriptions derive from XML-backed generated prose and qualified API/version
  identity. Maintain the original XML contract when an API changes; never add a
  parallel description to generated API Markdown or edit published HTML.
- Authored guide descriptions use nonempty string `description:` frontmatter when
  present, otherwise substantive introductory prose. Update an explicit description
  with the guide, or remove it to use the generated default.
- Keep source descriptions plain text. Escape them at rendering boundaries without
  altering page content, headings, navigation, URLs or source metadata.
- Preserve the homepage project description and truthful identity-only fallbacks
  for reference pages without introductory prose. Do not invent API behavior or
  require artificial uniqueness.
- Metadata changes must pass the hosted documentation metadata checks. Frozen
  published API snapshots require a separate reviewed maintenance operation;
  an ordinary documentation build does not regenerate them.
