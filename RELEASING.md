# Releasing Needlr

> **Full maintainer guide:** [`docs/releasing.md`](docs/releasing.md).

Needlr uses a protected-main release flow with two separate operations:

1. Prepare all release content in a pull request.
2. After the pull request is squash-merged and same-commit `main` CI succeeds,
   create and push only the version tag.

`scripts/release.ps1` performs the second operation. It never changes
`version.json`, creates a commit, pulls/rebases, or pushes a branch.

## TL;DR — cut a new alpha

### 1. Prepare the release pull request

```powershell
git fetch origin main --tags
git switch --create release/prepare-v0.0.3-alpha.3 origin/main

# Choose the next unused version, then update version.json on this branch.
nbgv set-version 0.0.3-alpha.3
```

In the same pull request:

- Move every rule row from `src/**/AnalyzerReleases.Unshipped.md` into
  the matching `AnalyzerReleases.Shipped.md`.
- Use the base-version analyzer header, such as `## Release 0.0.3`.
  Roslyn rejects prerelease labels in analyzer release headers.
- Move the applicable `CHANGELOG.md` content into an exact dated section:
  `## [0.0.3-alpha.3] - YYYY-MM-DD`.
- Update version-specific documentation when applicable.
- Run `scripts/test-release.ps1` and the normal targeted validation.

Push the feature branch, open a pull request, wait for required CI, and
squash-merge it. Do not push the preparation commit directly to `main`.

### 2. Finalize from synchronized main

Wait for the `ci.yml` push run on the squash-merge commit to succeed, then:

```powershell
git fetch origin main --tags
git switch main
git pull --ff-only origin main

./scripts/release.ps1 0.0.3-alpha.3 -DryRun
./scripts/release.ps1 0.0.3-alpha.3
```

The real run validates the prepared commit, packs the solution, validates
package contents, runs `nbgv tag`, and executes only:

```text
git push origin refs/tags/v0.0.3-alpha.3
```

The tag triggers `.github/workflows/release.yml`. That workflow independently
requires a successful `ci.yml` push run for the exact tag commit before it can
publish packages or documentation.

## Gates enforced by `scripts/release.ps1`

| Gate | Requirement |
|---|---|
| Clean working tree | No tracked, staged, or untracked changes |
| NBGV available | `nbgv` is on `PATH` or under `~/.dotnet/tools/` |
| Prepared version | `version.json` and NBGV both resolve exactly to the requested version |
| Changelog | Exact `## [<version>]` section exists |
| Analyzer tracking | Every `AnalyzerReleases.Unshipped.md` has zero rule rows |
| Unused version | The version tag does not exist locally or on `origin` |
| Protected-main position | Real runs use local `main` exactly equal to freshly fetched `origin/main` |
| Same-commit CI | The successful `ci.yml` push run is for that exact `main` commit |
| Build + pack | `dotnet pack src/NexusLabs.Needlr.slnx -c Release` succeeds |
| Package contents | `scripts/test-packages.ps1 -NoBuild` succeeds |
| Final race check | `main` and tag availability are checked again immediately before tagging |

There is no CI bypass. A release that is not on successful, synchronized
`main` is not ready to tag.

## Version source of truth

- `version.json` is the build-time source of truth.
- Use `nbgv set-version <version>` only on the release-preparation branch.
- The squash-merge commit must be the commit that introduces that version so
  NBGV resolves the exact release version without a commit-height suffix.
- Tags are lightweight `v<SemVer>` tags, for example
  `v0.0.3-alpha.3`.

## After the release

- Verify the release workflow succeeded.
- Verify the packages on
  [NuGet.org](https://www.nuget.org/packages/NexusLabs.Needlr).
- Verify the GitHub Release assets and notes.
- Verify stable/versioned API documentation.

See [`docs/releasing.md`](docs/releasing.md) for preparation details,
troubleshooting, and rollback guidance.
