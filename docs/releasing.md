# Releasing Needlr

This is the in-depth maintainer guide for cutting a new Needlr release
(alpha, beta, rc, or stable). It exists so nobody — human or LLM
assistant — has to rediscover the release process by reading commit
history.

The fast-lookup version lives at [`RELEASING.md`](https://github.com/ncosentino/needlr/blob/main/RELEASING.md) in the repo root.

---

## What a release actually is

A Needlr release is:

1. A move of every unshipped analyzer diagnostic from
   `AnalyzerReleases.Unshipped.md` → `AnalyzerReleases.Shipped.md`.
2. A `CHANGELOG.md` entry under a new `## [x.y.z-label.N]` heading.
3. A version bump in `version.json` via Nerdbank.GitVersioning.
4. A lightweight git tag `v<version>` on the bump commit.
5. A push of the tag to `origin`, which triggers
   `.github/workflows/release.yml` to build, test, pack, publish to
   NuGet.org + GitHub Packages, and create a GitHub Release.

Steps 1-4 are orchestrated by `scripts/release.ps1`. Step 5 is the CI
workflow. Everything after the tag push is automated.

---

## Prerequisites

You need all of the following installed and working before running
the release script:

| Tool | Purpose | Install |
|---|---|---|
| .NET 10 SDK | Build + pack | [dot.net](https://dot.net) |
| PowerShell 7+ (`pwsh`) | Runs `release.ps1` | [aka.ms/pwsh](https://aka.ms/pwsh) |
| `nbgv` | Bumps `version.json` and tags | `dotnet tool install -g nbgv` |
| `gh` CLI | CI gate queries GitHub check runs | [cli.github.com](https://cli.github.com) |
| Python + mkdocs (optional) | Local docs validation | `pip install -r docs/requirements.txt` |

You also need:

- **Push access to `origin/main`** on `ncosentino/needlr`.
- **A NuGet.org API key** stored as `NUGET_API_KEY` in the GitHub
  repository secrets (already set — the workflow reads it).
- **The GitHub-provided `GITHUB_TOKEN`** (automatic, no setup).

---

## Version numbering

Needlr uses [SemVer 2.0.0](https://semver.org/) with a specific tag
format enforced by `version.json`.

### Source of truth

`version.json` at the repo root contains the version Nerdbank.GitVersioning
resolves at build time. Every `.csproj` inherits from it — **there are
zero hardcoded versions in individual project files**.

```json
{
  "version": "0.0.2-alpha.25",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/heads/release/v\\d+\\.\\d+",
    "^refs/tags/v\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z\\.]+)?$"
  ]
}
```

Do not edit `version.json` by hand during a release. Always go through
`nbgv set-version` or `release.ps1 -Prerelease alpha`.

### Tag format

Tags are lightweight (not annotated) and follow the pattern:

```
v<major>.<minor>.<patch>-<label>.<counter>
```

For alpha: `v0.0.2-alpha.25`, `v0.0.2-alpha.26`, ...

**Watch the separator:** it's a **dot** between `alpha` and the
counter, not a dash. `v0.0.2-alpha-0026` is wrong; `v0.0.2-alpha.26` is
right. NuGet displays the version with different normalization in its
UI but the tag and `version.json` use the dotted form.

### Finding the next version

```powershell
./scripts/release.ps1 -Prerelease alpha -Base 0.0.2 -DryRun
```

The `-Prerelease` flag scans existing tags for the highest
`v0.0.2-alpha.*` and increments the counter. `-Base` pins the base
version so a stale `version.json` doesn't confuse the calculation. The
dry run prints what the real run would do.

---

## Pre-release gates

Before the script takes any destructive action (version bump, commit,
tag, push) it runs the following gates. All must pass.

### 1. Clean working tree

```powershell
git diff --quiet
```

Dirty trees are rejected. Stash or commit first. This exists because
`release.ps1` uses `git commit -am` for the version bump — any
unrelated staged or unstaged changes would get dragged into the release
commit.

### 2. `nbgv` installed

The script checks `Get-Command nbgv` and falls back to
`~/.dotnet/tools/nbgv.exe`. If neither is present it throws a
remediation message pointing at `dotnet tool install -g nbgv`.

### 3. CI green on HEAD

```powershell
gh api "repos/$repoSlug/commits/$sha/check-runs"
```

Every check run on the current commit must be `completed` with
`conclusion` in `success` / `skipped` / `neutral`. Anything failing,
pending, or neutral blocks the release.

The rationale: the CI workflow on `main` runs the full test matrix
(unit tests, integration tests, generator tests, AspNet tests, AOT
publish, example builds). If any of those are red, the package you're
about to ship is known-broken.

Override with `-SkipCiCheck` only if you're deliberately cutting a
release before CI finishes (for example, you just pushed a fix and
don't want to wait five minutes, and you've personally verified the
build locally). Never skip this on a release candidate or stable.

### 4. Analyzer release tracking

> This is the gate that was silently broken before
> [`f66653c2`](https://github.com/ncosentino/needlr/commit/f66653c2). If
> you are reading this because a past release shipped with stale
> unshipped rules, welcome back.

Every Needlr analyzer project includes two `AdditionalFiles`:

- `AnalyzerReleases.Shipped.md` — every diagnostic ID that has shipped
  in a prior release, grouped under `## Release <version>` headers.
- `AnalyzerReleases.Unshipped.md` — every diagnostic ID that is in code
  but has not yet been included in a released version.

`Microsoft.CodeAnalysis.Analyzers` (referenced from every analyzer
project) enforces these files via rules **RS2000**, **RS2001**, and
**RS2002**:

- **RS2000**: Add the new rule to `Unshipped.md` when you introduce it
  in code.
- **RS2001**: Rule IDs in `Unshipped.md` must eventually move to
  `Shipped.md` before a release.
- **RS2002**: Rules in `Shipped.md` must still exist in the analyzer.

**Before every release**, every unshipped rule must move. The release
script refuses to proceed otherwise:

```
BLOCKED: analyzer projects have unshipped rules.

Before releasing, move each rule below from its AnalyzerReleases.Unshipped.md
file into the matching AnalyzerReleases.Shipped.md under a new header:
  ## Release 0.0.2-alpha.26
```

See [Shipping analyzers](#shipping-analyzers) below for the exact
mechanical procedure.

### 5. Build + pack validation

The script walks every `.csproj` under `src/` that is neither a test
project nor has `<IsPackable>false</IsPackable>`, and runs
`dotnet pack -c Release -v q --no-restore` on each. First failure
aborts the release. This catches:

- Projects that compile for `dotnet build` but fail at pack time
  (missing `Description`, invalid package id, missing README).
- Projects that ship a new analyzer DLL via a custom `None Include`
  entry whose `OutputPath` doesn't exist yet.
- Projects whose `netstandard2.0` target drifts against the generator
  requirements.

### 6. Nuspec validation

After successful packs, `scripts/test-packages.ps1 -NoBuild` runs. It
extracts every `.nupkg` from `artifacts/`, parses the embedded
`.nuspec`, and asserts:

- The `dependencies` graph matches expected shape.
- Analyzer and generator DLLs are placed at the correct package paths
  (`analyzers/dotnet/cs/`).
- The `Needlr.Build` package correctly transitively delivers the
  generator assembly (regression test for a past bug — see commit
  `b77544fa`).

This is the last gate before the version bump actually happens.

---

## Shipping analyzers

This is the step that historically got forgotten. `release.ps1` now
blocks on it, but you still have to do the mechanical move yourself.

### Finding what needs to ship

The guardrail tells you exactly which files and which rule IDs. You
can also check manually:

```powershell
Get-ChildItem src -Recurse -Filter AnalyzerReleases.Unshipped.md |
  ForEach-Object {
    $rules = Get-Content $_.FullName | Where-Object { $_ -match '^NDLR' }
    if ($rules) {
      Write-Host $_.FullName
      $rules | ForEach-Object { Write-Host "  $($_ -split '\s*\|\s*' | Select-Object -First 1)" }
    }
  }
```

### The mechanical move

For each `AnalyzerReleases.Unshipped.md` with unshipped rules:

1. Open the paired `AnalyzerReleases.Shipped.md`.
2. Prepend a new section **at the top** (after the comment header,
   before any existing `## Release` section):

   ```markdown
   ## Release 0.0.2-alpha.26

   ### New Rules

   Rule ID | Category | Severity | Notes
   --------|----------|----------|-------
   <paste every unshipped row here, unchanged>
   ```

3. Open `AnalyzerReleases.Unshipped.md` and **delete only the rule
   data rows**. Keep:
   - The `; Shipped analyzer releases` comment at the top
   - The help link comment
   - The `### New Rules` heading
   - The table header row (`Rule ID | Category | Severity | Notes`)
   - The separator row (`--------|----------|----------|-------`)

   The post-ship file should look like:

   ```markdown
   ; Unshipped analyzer releases
   ; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

   ### New Rules

   Rule ID | Category | Severity | Notes
   --------|----------|----------|-------
   ```

4. Repeat for each analyzer project with unshipped rules.

5. Build the solution locally once to verify the analyzers themselves
   accept the updated files:

   ```powershell
   dotnet build src/NexusLabs.Needlr.slnx -c Release --nologo
   ```

   If `Microsoft.CodeAnalysis.Analyzers` rejects your edit it will
   emit an RS2001 or RS2002 error. Fix before proceeding.

6. Commit with the conventional message:

   ```
   chore: ship analyzers for 0.0.2-alpha.26
   ```

### Version header format

Past attempts to match the version header format have bounced a few
times (see fix commits `83ef38ab`, `6b7e1166`, `22bd5b64`). The
authoritative format is:

```
## Release <version>
```

Where `<version>` matches the version being released — for an alpha
that's `0.0.2-alpha.26`. Do not include the `v` prefix, do not include
a date, do not use a dash between `alpha` and the counter.

Example from commit `6e0b08bb` (`chore: ship analyzers for
0.0.2-alpha.17`):

```markdown
## Release 0.0.2-alpha.17

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NDLRCOR003 | NexusLabs.Needlr | Error | DeferToContainerInGeneratedCodeAnalyzer, ...
NDLRCOR004 | NexusLabs.Needlr | Warning | GlobalNamespaceTypeAnalyzer, ...
```

### If you add a new analyzer diagnostic between releases

1. Add the `DiagnosticDescriptor` in the analyzer project.
2. Add a row to that project's `AnalyzerReleases.Unshipped.md` under
   `### New Rules` with the same `Rule ID | Category | Severity | Notes`
   format. **Build immediately** — if you forget, the next release
   script run will tell you, but it's much easier to fix at the source.
3. Write the doc page at `docs/analyzers/NDLRXXX.md` following the
   template in `.claude/rules/generated/docs.md`.
4. Add a nav entry in `mkdocs.yml` under the appropriate analyzer
   subgroup.
5. Add a row to `docs/analyzers/README.md` in the matching table.

The release script handles the shipping; you only have to remember the
Unshipped.md row on the day you add the rule.

---

## Writing the CHANGELOG entry

`CHANGELOG.md` follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

The release workflow extracts release notes by searching for a section
matching `## [<version>]` — it takes everything from that header up
until the next `## [` header.

### Template

```markdown
## [0.0.2-alpha.26] - 2026-04-10

### Added

- **Feature name** — One-sentence description. Link to the PR or
  feature docs if relevant.

### Fixed

- **Bug description** — What broke, how it was fixed, observable
  symptom before the fix. Include issue/PR links.

### Changed

- Internal refactors or breaking changes. Breaking changes should be
  clearly flagged.

### Shipped analyzers

- `NDLRXXX001`, `NDLRXXX002`, ... (list every ID moved to Shipped.md
  in this release, even if they were added in a prior alpha).
```

The `### Shipped analyzers` section is a Needlr convention. It lets
downstream consumers see at a glance which diagnostic IDs are now
"released" in a given version, which helps them triage RS2000 errors
that pop up when they upgrade.

### Finding what changed

```powershell
# Commits since the previous release tag.
git log v0.0.2-alpha.25..HEAD --oneline
```

Turn that list into CHANGELOG sections manually, or use the
`skills/changelog-generator/scripts/generate.py` helper referenced by
the dry-run output:

```powershell
python skills/changelog-generator/scripts/generate.py \
  --from v0.0.2-alpha.25 \
  --to HEAD \
  --version 0.0.2-alpha.26
```

Review the output, edit for tone, and append to `CHANGELOG.md`.

---

## Running the release

### Dry run first

```powershell
./scripts/release.ps1 -Prerelease alpha -Base 0.0.2 -DryRun
```

Dry run:

- Runs every gate listed in [Pre-release gates](#pre-release-gates)
  except the clean-working-tree check (so you can iterate).
- Computes the next version number.
- Extracts the `CHANGELOG.md` entry for that version and prints it.
- Prints what it would commit, tag, and push — without doing any of it.

If the dry run reports a missing CHANGELOG entry or unshipped
analyzers, fix those and re-run.

### Real run

```powershell
./scripts/release.ps1 -Prerelease alpha -Base 0.0.2
```

The real run, in order:

1. Runs all gates.
2. Runs `nbgv set-version <new>` to bump `version.json`.
3. Creates a commit: `chore: bump version to 0.0.2-alpha.26`.
4. Creates a lightweight tag via `nbgv tag`.
5. Runs `git push origin HEAD --tags`.

### When the tag lands on origin

`.github/workflows/release.yml` fires on the tag push. Its steps:

1. Checkout + setup .NET 10.
2. Restore, build `src/NexusLabs.Needlr.slnx` with `-p:PublicRelease=true`.
3. Run full test suite with coverage collection.
4. Pack every `NexusLabs.Needlr*.csproj` except tests, benchmarks,
   integration tests.
5. `dotnet nuget push` every `.nupkg` to NuGet.org using
   `${{ secrets.NUGET_API_KEY }}` with `--skip-duplicate`.
6. `dotnet nuget push` every `.nupkg` to GitHub Packages using
   `${{ secrets.GITHUB_TOKEN }}` with `--skip-duplicate`.
7. Extract the matching `## [<version>]` section from `CHANGELOG.md`.
8. Create a GitHub Release via `softprops/action-gh-release@v2`
   flagged as pre-release because the tag contains `-`, attaching
   every `.nupkg` and `.snupkg` file.

Watch the workflow run at
[Actions](https://github.com/ncosentino/needlr/actions/workflows/release.yml)
while it runs. It typically takes 6-10 minutes.

---

## Post-release verification

After the workflow succeeds:

1. **NuGet.org:** visit
   [nuget.org/packages/NexusLabs.Needlr](https://www.nuget.org/packages/NexusLabs.Needlr)
   and verify the new version appears under the Versions tab. Check a
   few other key packages too (`NexusLabs.Needlr.AspNet`,
   `NexusLabs.Needlr.Generators`, `NexusLabs.Needlr.AgentFramework`).
2. **GitHub Release:** visit
   [releases](https://github.com/ncosentino/needlr/releases) and verify
   the new tag shows up, is marked as pre-release, has the CHANGELOG
   section as the release notes, and has the `.nupkg` + `.snupkg`
   assets attached.
3. **GitHub Packages:** visit
   [Packages](https://github.com/ncosentino/needlr/packages) and verify
   the new version is present.
4. **Smoke test:** create a scratch project that references the new
   version and verifies the most critical feature still works. For a
   web-path fix like `v0.0.2-alpha.26`, that means running the
   `MinimalWebApiSourceGen` example against the new package reference.

If any verification step fails, open an issue immediately and start
the rollback conversation. See [Rolling back](#rolling-back).

---

## Rolling back

NuGet.org packages can be **unlisted** (hidden from search and version
resolution) but not deleted. GitHub Releases can be deleted. Tags can
be deleted (but doing so does not unpublish the NuGet packages).

If a released version is broken:

1. **Unlist the bad version on NuGet.org** via the web UI
   (Manage Package → Listing). This stops new consumers from pulling
   it but preserves package integrity for anyone who already has it
   cached.
2. **Delete the GitHub Release** (optional, only if the release page
   itself is misleading).
3. **Delete the local and remote tag** (optional, only if the tag
   commit itself needs to be revised):
   ```powershell
   git tag -d v0.0.2-alpha.26
   git push origin :refs/tags/v0.0.2-alpha.26
   ```
4. **Cut a new release** with the fix and a higher counter
   (`v0.0.2-alpha.27`). Never re-use a version number that has been
   pushed to NuGet.org.
5. **Write a CHANGELOG entry** for the new release explaining the
   rollback and what was broken in the unlisted version.

Unlisting is cheap and reversible; deletion is not. Prefer unlist.

---

## Troubleshooting

### `nbgv: command not found`

Install the tool:

```powershell
dotnet tool install -g nbgv
```

Make sure `~/.dotnet/tools` is on your `PATH` (the script also looks
there directly as a fallback).

### `BLOCKED: CI is not fully green on HEAD`

One of the GitHub check runs for the current commit is failing,
pending, or neutral. Open the Actions tab for the commit on GitHub,
find the failing run, fix the underlying issue, push, wait for the
re-run to go green, then re-run the release script.

Override with `-SkipCiCheck` only in true emergencies and only when
you've personally verified the build and tests locally.

### `BLOCKED: analyzer projects have unshipped rules`

See [Shipping analyzers](#shipping-analyzers). The script prints every
file and every rule ID that needs to be moved; just walk the list.

### `Pack validation failed`

Some `.csproj` failed `dotnet pack`. Run the failing pack command
manually to see the full error:

```powershell
dotnet pack src/NexusLabs.Needlr.<Whatever>/NexusLabs.Needlr.<Whatever>.csproj -c Release
```

Common causes:

- Missing `<Description>` property on a new project.
- `PackageReadmeFile` pointing at a README that wasn't marked as
  included in the pack.
- A newly-added analyzer DLL whose `OutputPath` doesn't resolve because
  the build matrix is wrong (check the `<None Include>` entry in the
  `.csproj`).

### Nuspec validation fails

`scripts/test-packages.ps1` caught a packaging regression. Look at the
specific assertion that fired in the output. Most common: a change to
`Directory.Build.props` that broke a transitive dependency for one of
the bundle packages.

### The workflow fires but nothing publishes

Check the Actions tab for the workflow run. Common causes:

- The `NUGET_API_KEY` secret expired. Renew at
  [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
  and update the secret in repo settings.
- A transient NuGet.org outage. Re-run the workflow from the Actions
  tab.
- A package was already published at that version (`--skip-duplicate`
  in the push command silently swallows this — it's usually a sign
  someone manually published before the workflow ran, which shouldn't
  happen).

### Docs build fails in `mkdocs` strict mode

If your release adds new analyzer diagnostic doc pages, ensure:

- Each new `docs/analyzers/NDLRXXX.md` exists.
- Each new page has a nav entry in `mkdocs.yml` under the correct
  analyzer subgroup.
- Each new page has a row in `docs/analyzers/README.md`.

This is documented in `.claude/rules/generated/docs.md`. The `api/stable/*`
warnings emitted by strict mode are pre-existing and expected locally;
CI handles them with a placeholder step.

---

## Historical gotchas (why this document exists)

Every item here has bitten past releases. They're listed not to shame
anybody, but so the next maintainer can recognize the failure mode:

- **Forgetting the analyzer Unshipped→Shipped move.** Commits
  `83ef38ab`, `6b7e1166`, `22bd5b64`, `6e0b08bb`, `22c7e284` are all
  retroactive fixes or ship-catch-up commits. This is the single most
  common release-day mistake, which is why
  `scripts/release.ps1` now blocks on it.
- **Wrong version header format.** Early releases used
  `## Release <date>` or omitted the version entirely. The correct
  format is `## Release <version>` with no date, no `v` prefix.
- **Using `alpha-0026` instead of `alpha.26`.** The dot-separated form
  is what `version.json`, `nbgv`, and the release workflow expect.
  NuGet displays it differently in the web UI but the source of truth
  uses the dot.
- **Pushing to `origin/main` before CI runs.** Always wait for CI to
  go green on the version-bump commit before the tag push. The release
  script's CI gate enforces this for you.
- **Releasing from a dirty working tree.** Early releases accidentally
  included uncommitted changes in the version bump commit. The clean
  tree gate catches this now.

---

## See also

- [`RELEASING.md`](https://github.com/ncosentino/needlr/blob/main/RELEASING.md) — the
  fast-lookup version at the repo root.
- [`scripts/release.ps1`](https://github.com/ncosentino/needlr/blob/main/scripts/release.ps1)
- [`.github/workflows/release.yml`](https://github.com/ncosentino/needlr/blob/main/.github/workflows/release.yml)
- [`CHANGELOG.md`](https://github.com/ncosentino/needlr/blob/main/CHANGELOG.md)
- [Roslyn analyzer release tracking docs](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md)
