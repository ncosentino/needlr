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

CI runs `python -m mkdocs build` directly — no placeholder step. `docs/api/stable/index.md` is a **tracked static placeholder** on main (see file header) that exists only to give mkdocs a valid source for `/api/stable/`. The real stable API reference lives exclusively on the `gh-pages` branch and is preserved forever by `peaceiris/actions-gh-pages@v4`'s `keep_files: true`.

To verify locally:

```bash
python -m mkdocs build --strict
```

## Docs pipeline ownership (ci.yml vs release.yml)

Both workflows deploy to `gh-pages` with `keep_files: true`, and each owns a disjoint slice. Before deploying, each strips the **other** workflow's slice from its `./site/` build output so the remote content is preserved.

| Path                 | Owner        |
|----------------------|--------------|
| `/api/dev/*`         | `ci.yml`     |
| `/coverage/*`        | `ci.yml`     |
| `/api/stable/*`      | `release.yml`|
| `/api/v<version>/*`  | `release.yml`|
| home, features, nav  | both (identical output) |

**Invariant**: neither workflow writes generated API docs back to `main`. Per-version `/api/v<version>/` directories are immutable once published and are preserved on `gh-pages` indefinitely.

**Catalog source of truth**: `scripts/generate-versioned-api-docs.sh` enumerates `/api/` versions from `git tag --list 'v*'` — NOT from walking `docs/api/v*/` in the working tree (which is blocked by `.gitignore` and always empty on main).
