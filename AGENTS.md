# Needlr — AI Agent Instructions

Needlr is an opinionated dependency injection framework for .NET. Compile-time source
generation is the primary discovery strategy; reflection is opt-in.

## Sources of truth

- Use the README and `docs/` for identity, architecture, rationale, and accepted
  decisions.
- Resolve exact edit rules with
  `pwsh scripts/guidance/Get-ApplicableInstructions.ps1 -Path <changed-paths>`.
- Treat `.github/instructions/genesis/` as generated and read-only. Specialize it only
  in project-owned instruction files outside that subtree.
- Code, manifests, schemas, tests, and workflows are executable truth.

## Safeguards

- Never write new source, docs, or scripts to the repository root.
- Keep this file below 60 lines and 3,072 UTF-8 bytes. Technical rules belong in
  path-scoped instructions, procedures in skills, and rationale in docs.
- Before public delivery, remove local paths, private context, credentials, and raw
  logs.

## Build and test

    dotnet build src/NexusLabs.Needlr.slnx
    dotnet test src/NexusLabs.Needlr.slnx

## Delivery

- Use feature branches and pull requests; never push directly to `main`.
- Target `main` unless `.github/genesis-delivery.json` explicitly enables stacked
  bases.
- Run `.github/skills/review-changes/SKILL.md` before publishing a ready pull request.
