---
description: Compile, test, and pack NexusLabs.Needlr from source.
---

# Building from Source

You need the [.NET SDK](https://dotnet.microsoft.com/download) installed. Check
`global.json` in the repository root for the exact version this project targets.

## Build

```bash
dotnet restore src/NexusLabs.Needlr.slnx
dotnet build src/NexusLabs.Needlr.slnx --configuration Release --no-restore
```

## Test

```bash
dotnet test src/NexusLabs.Needlr.slnx --configuration Release --no-build
```

Use a targeted test project or filter while iterating. Pull-request CI owns the full
package, documentation, integration, and Native AOT gates.

## Documentation

```bash
python -m pip install -r docs/requirements.txt
python -m mkdocs build --strict
```

### Layout checks and review screenshots

The hosted Documentation layout job covers both full CI and documentation-only
guidance validation. It checks the actual built homepage at 375px and 1440px in
both themes. It holds image responses, compares geometry before
and after release, and requires separate 44px footer targets with 4px gaps.
The regression checks use deliberately wide synthetic badges so they do not
depend on external badge services.

Manually dispatching CI on a feature branch additionally captures real-image
homepage loading states and footer screenshots for human review. Screenshots
are evidence, never screenshot assertions or pixel-comparison baselines.
The `documentation-layout` artifact retains bounded JSON and PNG evidence for
one day, capped at 20 MiB per run; ordinary regression evidence is much smaller.
Feature-branch validation does not deploy documentation.
