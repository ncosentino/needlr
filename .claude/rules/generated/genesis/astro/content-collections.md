---
# AUTO-GENERATED from .github/instructions/genesis/astro/content-collections.instructions.md — do not edit
paths:
  - "**/content.config.ts"
  - "**/content/**/*.md"
  - "**/content/**/*.mdx"
  - "**/content/**/*.json"
---
# Astro content collections

- Define every collection with `defineCollection` in `src/content.config.ts` and give
  it a loader plus Zod schema.
- Use `glob()` for one entry per Markdown/MDX/data file and `file()` for one structured
  array file.
- Every field consumed by templates exists in the schema; untyped content is forbidden.
- `file()` entries have stable unique ids. Add/sort an explicit `order` field when
  display order matters.
- Markdown/MDX frontmatter matches the collection schema.
- Query with `getCollection()`, not raw imports. Production queries filter drafts and
  apply deliberate ordering.
- Use Markdown for prose and MDX only when interactive components are required.
- Each content item supplies unique title/description and accurate visible metadata/
  structured data.
