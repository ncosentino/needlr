---
applyTo: "src/**/*.cs,docs/**/*.md,docs/hooks/**/*.py,overrides/**,mkdocs.yml,scripts/test-docs-titles.py,scripts/refresh-docs-titles.py"
---

# Documentation Metadata

- Maintain API XML contracts; generated descriptions reuse them. Never hand-edit generated API docs or published HTML.
- Update guide `description:` frontmatter with its source, or omit it for an introductory-prose default.
- Preserve source content and metadata; escape descriptions only at rendering boundaries.
- Follow the source ownership and hosted checks documented in `docs/development/documentation-metadata.md`.
