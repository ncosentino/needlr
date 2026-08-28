---
applyTo: "docs/**/*.md,mkdocs.yml"
---

# Documentation Rules

## mkdocs strict mode

`mkdocs.yml` has `strict: true`, but MkDocs reports an unlisted page at `INFO` and still
exits successfully. `scripts/test-guidance.ps1` is the enforcement owner: every
`docs/**/*.md` file must appear in `nav:`. Add every new page to the navigation.

## New feature documentation

Every new source-generated feature needs:

1. A feature page in `docs/<feature>.md` (structure: Quick Start, detailed sections, Attribute Reference table, Analyzers table)
2. A bullet in `docs/getting-started.md` under "Next Steps"
3. A nav entry in `mkdocs.yml` under the "Features" section

## New analyzer diagnostics

Every new diagnostic needs ALL THREE:

1. `docs/analyzers/NDLRXXX.md` — sections: `# NDLRXXX: Title`, Cause, Rule Description, How to Fix (with `// CORRECT` / `// WRONG` code blocks), See Also
2. A nav entry in `mkdocs.yml` under the appropriate "Analyzers" subgroup
3. A row in `docs/analyzers/README.md` in the relevant table

## CI docs build

CI runs `python -m mkdocs build --strict`. The tracked
`docs/api/stable/index.md` placeholder gives MkDocs a source for `/api/stable/`; the
published stable API reference remains on `gh-pages`.

To verify locally:

```bash
python -m mkdocs build --strict
```
