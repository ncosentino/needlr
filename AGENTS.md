# Needlr — AI Agent Instructions

Needlr is an opinionated dependency injection framework for .NET. Compile-time source
generation is the primary discovery strategy; reflection is opt-in.

## Before you touch anything

- **Never write to the repository root.** Source lives under `src/`, docs under `docs/`,
  scripts under `scripts/`.
- **Never add technical rules to this file.** It loads in every session and is capped at
  60 lines / 3072 bytes, enforced by `scripts/test-agent-root-files.ps1`. Put rules in a
  path-scoped instruction file whose glob matches the code the rule governs.
- **Never edit `.github/instructions/genesis/`.** Those files are replaced by sync. Add
  project specialization in a sibling file outside that directory.

## Build and test

    dotnet build src/NexusLabs.Needlr.slnx
    dotnet test src/NexusLabs.Needlr.slnx

## Where the rules live

Path-scoped rules in `.github/instructions/**/*.instructions.md` activate automatically
from their `applyTo` glob. Read the file that matches what you are editing.

| Editing | File |
|---|---|
| Any `.cs` | `csharp-conventions.instructions.md` |
| Source generators | `source-generators.instructions.md` |
| Anything reaching `AddSource` | `generator-determinism.instructions.md` |
| Discovery helpers | `discovery-helpers.instructions.md` |
| Generator models | `models.instructions.md` |
| Attributes package | `attributes.instructions.md` |
| Integration tests | `integration-tests.instructions.md` |
| Project and props files | `project-files.instructions.md` |
| Docs and `mkdocs.yml` | `docs.instructions.md` |
| Examples | `examples.instructions.md` |
| Workflows and CI | `needlr/hosted-ci.instructions.md` |

## Key documents

- Feature layer pattern: `docs/architecture/feature-layers.md` — a new source-generated
  feature needs every layer, including the analyzer, docs, and integration tests.
- Architecture decisions: `docs/architecture/decisions.md` — check existing ADRs before
  implementing, and propose one when a choice is costly to reverse.
- Delivery: `docs/development/delivery.md` — branch and PR rules, draft versus ready CI
  scope, review policy, and the disclosure required before marking a PR ready.

Deliver every change through a feature branch and pull request. Never push to `main`.