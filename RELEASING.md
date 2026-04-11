# Releasing Needlr

> **Full maintainer guide:** [`docs/releasing.md`](docs/releasing.md).
>
> This file is the short, fast-lookup version for muscle memory. The
> in-depth guide explains every step, every gate, and every common
> failure mode.

## TL;DR — cut a new alpha

```powershell
# 1. Start from a clean tree on main with CI green on origin.
git checkout main
git pull

# 2. Ship analyzers. Move every rule out of
#    src/**/AnalyzerReleases.Unshipped.md into the matching
#    AnalyzerReleases.Shipped.md under a NEW header:
#        ## Release 0.0.2-alpha.N
#    Commit as: chore: ship analyzers for 0.0.2-alpha.N
#    (scripts/release.ps1 refuses to proceed without this.)

# 3. Add a CHANGELOG.md entry:
#        ## [0.0.2-alpha.N] - YYYY-MM-DD
#    with Added / Fixed / Changed sections.

# 4. Dry-run the release script to validate every gate passes.
./scripts/release.ps1 -Prerelease alpha -Base 0.0.2 -DryRun

# 5. Run for real. This bumps version.json, commits, tags, and pushes.
./scripts/release.ps1 -Prerelease alpha -Base 0.0.2
```

Pushing the `v0.0.2-alpha.N` tag to `origin` triggers
`.github/workflows/release.yml`, which:

1. Builds the solution with `-p:PublicRelease=true`.
2. Runs the full test suite with coverage.
3. Packs every `NexusLabs.Needlr*.csproj` except `*.Tests`, `*.Benchmarks`, `*IntegrationTests`.
4. Pushes all `.nupkg` + `.snupkg` files to NuGet.org and GitHub Packages.
5. Creates a GitHub Release (flagged pre-release because the tag contains `-`)
   with release notes extracted from `CHANGELOG.md`.

## Gates enforced by `scripts/release.ps1`

The script refuses to proceed unless **all** of these pass:

| Gate | What it checks | Bypass flag |
|---|---|---|
| Clean working tree | `git diff --quiet` is clean | _(none — fix the tree)_ |
| `nbgv` CLI present | `nbgv` on PATH or under `~/.dotnet/tools/` | _(install it)_ |
| CI green on HEAD | `gh api check-runs` for current commit — every run `completed` + `success`/`skipped`/`neutral` | `-SkipCiCheck` |
| **Analyzer release tracking** | every `AnalyzerReleases.Unshipped.md` has zero rule rows | _(none — ship them)_ |
| Build + pack | `dotnet pack` succeeds for every packable project | _(fix the error)_ |
| Nuspec validation | `scripts/test-packages.ps1 -NoBuild` passes | _(fix the manifest)_ |

If any gate fails, the script throws before touching `version.json` or git.

## Version source of truth

- `version.json` (Nerdbank.GitVersioning) — the single file the build
  reads at compile time. Do not edit by hand; use `nbgv set-version` or
  `release.ps1`.
- Tag format: lightweight `v<SemVer>` with a dot before the prerelease
  counter: `v0.0.2-alpha.26`, not `v0.0.2-alpha-0026`.
- The release workflow's public-release regex is in `version.json` under
  `publicReleaseRefSpec`.

## After the release

- Verify the packages appear on
  [nuget.org/packages/NexusLabs.Needlr](https://www.nuget.org/packages/NexusLabs.Needlr).
- Verify the GitHub Release page has the `.nupkg` + `.snupkg` attachments.
- Verify the release notes section matches the CHANGELOG entry.

See [`docs/releasing.md`](docs/releasing.md) for troubleshooting, first-time
setup, and full rationale behind each gate.
