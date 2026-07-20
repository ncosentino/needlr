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
#    AnalyzerReleases.Shipped.md under the EXISTING base-version
#    release section (e.g. "## Release 0.0.3"). Keep rule IDs in
#    alphanumeric order within the section.
#
#    The header uses the base version ONLY — RS2007 rejects
#    pre-release labels, so "## Release 0.0.3-alpha.2" will not
#    build. All alpha/beta/rc releases of 0.0.3 share one section.
#
#    Commit as: chore: ship analyzers for 0.0.3-alpha.N
#    (scripts/release.ps1 refuses to proceed without this.)

# 3. Add a CHANGELOG.md entry:
#        ## [0.0.3-alpha.N] - YYYY-MM-DD
#    with Added / Fixed / Changed sections.

# 4. Dry-run the release script to validate every gate passes.
./scripts/release.ps1 -Prerelease alpha -Base 0.0.3 -DryRun

# 5. Run for real. This bumps version.json, pushes the commit to main,
#    then tags that exact commit.
./scripts/release.ps1 -Prerelease alpha -Base 0.0.3
```

Pushing the `v0.0.3-alpha.N` tag to `origin` triggers
`.github/workflows/release.yml`, which:

1. Waits for the `ci.yml` push run on `main` for the exact tag commit to
   complete successfully.
2. Builds the solution with `-p:PublicRelease=true`.
3. Runs the full test suite with coverage.
4. Packs every `NexusLabs.Needlr*.csproj` except `*.Tests`, `*.Benchmarks`, `*IntegrationTests`.
5. Exchanges the GitHub OIDC identity for a short-lived NuGet.org API key.
6. Pushes all `.nupkg` + `.snupkg` files to NuGet.org and GitHub Packages.
7. Creates a GitHub Release (flagged pre-release because the tag contains `-`)
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
  counter: `v0.0.3-alpha.2`, not `v0.0.3-alpha-0002`.
- The release workflow's public-release regex is in `version.json` under
  `publicReleaseRefSpec`.

## After the release

- Verify the packages appear on
  [nuget.org/packages/NexusLabs.Needlr](https://www.nuget.org/packages/NexusLabs.Needlr).
- Verify the GitHub Release page has the `.nupkg` + `.snupkg` attachments.
- Verify the release notes section matches the CHANGELOG entry.

See [`docs/releasing.md`](docs/releasing.md) for troubleshooting, first-time
setup, and full rationale behind each gate.
