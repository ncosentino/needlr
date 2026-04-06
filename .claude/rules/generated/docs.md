---
# AUTO-GENERATED from .github/instructions/docs.instructions.md — do not edit
paths:
  - "docs/**/*.md"
  - "mkdocs.yml"
---
# Documentation Rules

## mkdocs strict mode

`mkdocs.yml` has `strict: true`. Any `.md` file under `docs/` that is NOT listed in the `nav:` section will **abort the CI docs build**. Always add new pages to `nav:`.

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

CI runs `python -m mkdocs build` after applying a placeholder for `api/stable/index.md`. To verify locally:

```bash
python -m mkdocs build --strict
```

Pre-existing `api/stable/*` warnings are expected locally (CI handles them with a placeholder step).
