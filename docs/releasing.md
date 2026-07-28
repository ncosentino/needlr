# Releasing Needlr

This is the authoritative maintainer guide for cutting a Needlr release.
The fast-lookup checklist is
[`RELEASING.md`](https://github.com/ncosentino/needlr/blob/main/RELEASING.md).

## Release model

Needlr uses protected `main`. A release is deliberately split into two
operations:

1. **Release preparation through a pull request**
   - update `version.json`;
   - ship analyzer diagnostics;
   - create the exact `CHANGELOG.md` release section;
   - update version-specific documentation where applicable;
   - pass required pull-request CI.
2. **Tag-only finalization from synchronized `main`**
   - verify the prepared version and release metadata;
   - verify successful same-commit `main` CI;
   - validate build and package contents;
   - create and push only the version tag.

The split is a safety boundary. The release script cannot write a version
commit to protected `main`, and package publication cannot begin from a
commit that bypassed pull-request validation.

## What a release contains

A Needlr release consists of:

1. Analyzer rule rows moved from every
   `AnalyzerReleases.Unshipped.md` into the corresponding
   `AnalyzerReleases.Shipped.md`.
2. An exact dated `CHANGELOG.md` section:
   `## [x.y.z-label.N] - YYYY-MM-DD`.
3. The same version in `version.json`.
4. A lightweight `v<version>` tag on the squash-merge commit.
5. Automated build, test, packaging, publication, release creation, and
   documentation deployment from `.github/workflows/release.yml`.

The version, changelog, analyzer files, and documentation are reviewed in the
preparation pull request. `scripts/release.ps1` only validates and tags the
already-merged result.

## Prerequisites

| Tool | Purpose | Install |
|---|---|---|
| .NET 10 SDK | Build and pack | [dot.net](https://dot.net) |
| PowerShell 7+ (`pwsh`) | Run the release scripts | [aka.ms/pwsh](https://aka.ms/pwsh) |
| `nbgv` | Update `version.json`, resolve versions, and create the tag | `dotnet tool install -g nbgv` |
| `gh` CLI | Query the exact `main` CI workflow run | [cli.github.com](https://cli.github.com) |
| Python and MkDocs | Local documentation validation | `python -m pip install -r docs/requirements.txt` |

The maintainer needs permission to:

- create and merge a release-preparation pull request;
- push a version tag to `origin`;
- use the configured NuGet.org trusted-publishing policy indirectly through
  `release.yml`.

Direct push access to `main` is neither required nor permitted.

## Version numbering

Needlr uses SemVer 2.0.0. Prerelease tags use a dot before the counter:

```text
v0.0.3-alpha.3
```

Do not use `v0.0.3-alpha-0003`. NuGet may normalize the displayed package
version, but `version.json`, the changelog, and the git tag use the dotted
form.

### Source of truth

`version.json` is the version source read by every project. Individual project
files do not carry package versions.

Use `nbgv set-version <version>` on the release-preparation branch. The
tag-only release script intentionally does not call `nbgv set-version`.

Needlr is configured for squash-only merges. The squash-merge commit therefore
introduces the new `version.json` value and NBGV resolves exactly that version
on the merge commit. If NBGV reports a suffix such as `.g<sha>`, the current
commit is not the version-reset commit and must not be tagged.

### Choosing the next prerelease

Refresh `main`, then inspect the remote tag sequence without importing or
rewriting local tag refs:

```powershell
git fetch origin main
git ls-remote --tags --refs origin "refs/tags/v0.0.3-alpha.*"
```

Increment the highest published counter and confirm that neither the local nor
remote tag exists:

```powershell
git tag --list "v0.0.3-alpha.3"
git ls-remote --tags origin "refs/tags/v0.0.3-alpha.3"
```

The release script repeats both checks before tagging.

Do not require `git fetch --tags` for release preparation. Historical local
tags may intentionally or accidentally differ from remote tag objects, and Git
rejects an all-tags fetch rather than clobbering them. The release script reads
the authoritative remote sequence with `git ls-remote` and leaves every local
historical tag unchanged.

## Phase 1: prepare the release pull request

### Create the branch

Start from the latest remote `main`:

```powershell
git fetch origin main
git switch --create release/prepare-v0.0.3-alpha.3 origin/main
```

Never prepare the release by committing directly on local `main`.

### Update `version.json`

Run NBGV on the preparation branch:

```powershell
nbgv set-version 0.0.3-alpha.3
```

Review the resulting `version.json` diff. Do not manually change unrelated
NBGV settings.

### Ship analyzer diagnostics

Every Needlr analyzer project has:

- `AnalyzerReleases.Shipped.md` for diagnostics included in a release;
- `AnalyzerReleases.Unshipped.md` for diagnostics added since the last
  applicable release.

Find pending rule rows:

```powershell
Get-ChildItem src -Recurse -Filter AnalyzerReleases.Unshipped.md |
  ForEach-Object {
    $rules = Get-Content $_.FullName |
      Where-Object { $_ -match '^NDLR' }
    if ($rules) {
      Write-Host $_.FullName
      $rules
    }
  }
```

For each file with rule rows:

1. Open the paired `AnalyzerReleases.Shipped.md`.
2. Find or create the base-version section, such as:

   ```markdown
   ## Release 0.0.3

   ### New Rules

   Rule ID | Category | Severity | Notes
   --------|----------|----------|-------
   ```

3. Move every pending row into that section in alphanumeric diagnostic-ID
   order.
4. Delete only the rule data rows from `AnalyzerReleases.Unshipped.md`.
   Keep its comments, heading, table header, and separator.

The header uses the base version only. Roslyn rule RS2007 rejects a header such
as `## Release 0.0.3-alpha.3`.

The release script fails if any line beginning with `NDLR` remains in an
unshipped file.

### Create the changelog section

Move the content being released out of `## [Unreleased]` and into an exact,
dated section:

```markdown
## [0.0.3-alpha.3] - 2026-07-24

### Added

- ...

### Fixed

- ...

### Changed

- ...

### Shipped analyzers

- `NDLRGEN057`, `NDLRGEN058`, ...
```

The release workflow and `release.ps1` both require the exact
`## [<version>]` heading. The optional `### Shipped analyzers` section records
which diagnostics moved into the shipped files.

### Update version-specific documentation

Update any documentation whose examples, compatibility statements, or API
links refer to the release version. Routine feature documentation should
already be present before release preparation.

### Validate the preparation

Use the repository NuGet cache on this machine:

```powershell
$env:NUGET_PACKAGES = 'G:\dev\caches\nuget\packages'
```

Run the targeted release regression test:

```powershell
pwsh -NoProfile -File scripts/test-release.ps1
```

That test creates an isolated local remote, exercises dry-run and real
finalization with command shims, and proves:

- no branch commit is created;
- `origin/main` does not move;
- the expected tag is the only remote write;
- the tag points at the prepared `main` commit.

Then run the normal validation appropriate to the release content, including:

```powershell
dotnet build src/NexusLabs.Needlr.slnx -c Release
pwsh -NoProfile -File scripts/test-packages.ps1
python -m mkdocs build --strict
```

### Open and merge the pull request

Push only the feature branch and open the release-preparation pull request:

```powershell
git push -u origin release/prepare-v0.0.3-alpha.3
gh pr create
```

Make the pull request ready and wait for the stable required checks:

- `CI`, which summarizes `build-and-test`, `package-validation`,
  `aot-console-app`, and `aot-web-app`;
- `PR title`;
- `Review policy`.

A draft release-preparation pull request publishes `Draft CI`, which is not a
substitute for the ready pull request's full `CI` validation. During the delivery
migration transition, before GitHub branch protection is activated from
`.github/genesis-delivery.json`, the four summarized source-job contexts may also
remain temporarily required and must still pass.

Resolve review conversations and squash-merge the pull request. The
path-filtered `build-maui-example` workflow is not a required branch check;
when it runs for relevant changes, it must still pass.

## Phase 2: finalize the release tag

### Wait for same-commit main CI

After the preparation pull request merges, wait for the `ci.yml` **push** run
on the squash-merge commit to complete successfully. Pull-request CI is not a
substitute because the release workflow independently verifies the exact
`main` commit.

### Synchronize local main

```powershell
git fetch origin main
git switch main
git pull --ff-only origin main
git status --short
```

The status output must be empty. The script fetches again and requires:

```text
local HEAD == origin/main
```

It never pulls, rebases, commits, or pushes a branch on the maintainer's
behalf.

### Run the dry run

Use the exact prepared version:

```powershell
./scripts/release.ps1 0.0.3-alpha.3 -DryRun
```

Dry run validates:

- a completely clean working tree;
- NBGV availability;
- exact `version.json` and NBGV version agreement;
- exact changelog section;
- empty analyzer unshipped rule tables;
- local and remote tag availability.

It prints the real tag-only write operation. Dry run intentionally skips the
real-run-only main-position, hosted-CI, pack, and package-content gates.

The `-Prerelease` form remains available after the release version has already
been prepared:

```powershell
./scripts/release.ps1 -Prerelease alpha -Base 0.0.3 -DryRun
```

The computed version must still exactly match `version.json`.

### Run finalization

```powershell
./scripts/release.ps1 0.0.3-alpha.3
```

The real run:

1. Repeats every metadata and tag-availability check.
2. Requires local `main` to equal freshly fetched `origin/main`.
3. Requires a successful `ci.yml` push run for that exact SHA.
4. Runs solution-level Release pack validation.
5. Runs `scripts/test-packages.ps1 -NoBuild`.
6. Rechecks the clean tree, remote `main`, and tag availability to close race
   windows.
7. Runs `nbgv tag`.
8. Verifies the local tag resolves to `HEAD`.
9. Pushes only `refs/tags/v<version>`.

There is no `-SkipCiCheck` bypass. If same-commit `main` CI is missing,
pending, or failing, the release is not ready.

## Gates enforced by `release.ps1`

| Gate | Failure means |
|---|---|
| Clean repository | Tracked, staged, or untracked content could contaminate validation |
| NBGV installed | The prepared version or tag cannot be resolved reliably |
| Exact prepared version | `version.json`, NBGV, and the requested tag would disagree |
| Exact changelog section | Release notes are incomplete or use the wrong version |
| Analyzer release tracking | Diagnostics would ship while still marked unshipped |
| Tag availability | The version was already used or a tag race occurred |
| Synchronized protected main | The tag would not identify the reviewed merged commit |
| Successful same-commit CI | The exact release commit has not passed `main` CI |
| Solution pack | One or more packages cannot be built |
| Package assertions | A NuGet dependency or packaged asset regressed |
| Final race checks | `main`, the working tree, or tag state changed during validation |

Every gate fails closed. API errors, missing tools, and unparsable repository
identity are release blockers rather than warnings.

## What the tag triggers

Pushing `v<version>` starts `.github/workflows/release.yml`.

Before publication, the workflow:

1. Finds the `ci.yml` push run for `main` whose SHA equals the tag SHA.
2. Waits for that run to complete.
3. Fails unless its conclusion is `success`.
4. Checks that the tag version equals NBGV's semantic version.
5. Checks that the exact changelog section exists.
6. Restores, builds, tests, packs, validates package versions, and builds
   documentation.

After those gates, trusted publishing:

- exchanges the workflow OIDC identity for a short-lived NuGet.org key;
- pushes packages to NuGet.org and GitHub Packages;
- deploys stable and versioned API documentation;
- creates the GitHub Release and attaches package artifacts.

Tag pushes are separate from protected branch updates, so main protection does
not block release finalization.

## Post-release verification

After `release.yml` succeeds:

1. Verify the new version on
   [NuGet.org](https://www.nuget.org/packages/NexusLabs.Needlr).
2. Verify the GitHub Release is marked correctly and has `.nupkg` and
   `.snupkg` assets.
3. Verify the release notes match the exact changelog section.
4. Verify stable and versioned API documentation.
5. Perform a focused consumer smoke test when the released change warrants it.

## Troubleshooting

### `version.json contains '<old>', not '<new>'`

The release-preparation pull request did not update `version.json`, or local
`main` has not been synchronized after merge. Do not let the release script
change it. Prepare or merge the correct pull request, then update local
`main`.

### `NBGV resolves '<version>.g<sha>'`

The current commit is after the commit that introduced the version. A release
tag must point at the exact squash-merge commit that resets the version height.
Inspect the preparation pull request and repository merge method rather than
tagging the suffixed version.

### `Local main must exactly match origin/main`

Another commit reached `main`, or local `main` is behind/ahead. Fetch and use a
fast-forward update. If the new remote commit changes release content, review
it and wait for its same-commit CI before retrying.

### `No ci.yml push run exists`

Wait for the `main` push workflow to be created. Confirm the tag candidate is
the actual `main` SHA and that Actions is enabled.

### `Main CI must complete successfully`

Open the run URL printed by the script. Fix failures through another pull
request, merge it, update local `main`, and rerun all release checks.

### `BLOCKED: analyzer projects have unshipped rules`

Return to the preparation phase. Move every printed rule into the paired
shipped file under the base-version header, commit that change through a pull
request, and do not tag until it merges.

### Package validation fails

Run the failing command directly:

```powershell
dotnet pack src/NexusLabs.Needlr.slnx -c Release
pwsh -NoProfile -File scripts/test-packages.ps1 -NoBuild
```

Typical causes include missing package metadata, an incorrect analyzer asset
path, or a transitive dependency exclusion regression.

### The tag exists locally after a failed push

Inspect it before retrying:

```powershell
git show v0.0.3-alpha.3
git ls-remote --tags origin refs/tags/v0.0.3-alpha.3
```

If the remote tag does not exist and the local tag points at the correct,
unchanged `main` commit, retrying the push manually is possible. If there is
any mismatch, stop and investigate; never move or reuse a published version
tag casually.

## Rolling back a bad release

NuGet.org packages can be unlisted but not deleted.

1. Unlist the bad package version on NuGet.org.
2. Delete the GitHub Release only if its page is misleading.
3. Avoid deleting the tag unless the release record itself must be withdrawn.
4. Fix the defect through a new pull request.
5. Release a higher version. Never reuse a version that reached a package
   feed.

## Historical failure modes

- Forgetting to move analyzer rules before release.
- Using prerelease text in Roslyn analyzer release headers.
- Using `alpha-0003` instead of `alpha.3`.
- Tagging a commit whose NBGV version contains a commit-height suffix.
- Creating or pushing a release version commit directly on `main`.
- Treating pull-request CI as proof that the exact merged `main` commit passed.
- Releasing from a dirty repository.

The current process converts each failure mode into an explicit gate.

## See also

- [`RELEASING.md`](https://github.com/ncosentino/needlr/blob/main/RELEASING.md)
- [`scripts/release.ps1`](https://github.com/ncosentino/needlr/blob/main/scripts/release.ps1)
- [`scripts/test-release.ps1`](https://github.com/ncosentino/needlr/blob/main/scripts/test-release.ps1)
- [`.github/workflows/release.yml`](https://github.com/ncosentino/needlr/blob/main/.github/workflows/release.yml)
- [`CHANGELOG.md`](https://github.com/ncosentino/needlr/blob/main/CHANGELOG.md)
- [Roslyn analyzer release tracking](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md)
