---
description: Run Needlr CI/CD on standard GitHub-hosted runners.
---

# GitHub Actions CI/CD

Needlr runs all repository CI/CD jobs on standard GitHub-hosted runners. Workflows
declare their runner labels directly; the repository has no self-hosted runner profile,
runner-selection variable, or repository-owned runner image.

## Runner policy

The delivery contract permits only the runner labels used by the workflows:

| Workload | Runner |
| --- | --- |
| Linux build, test, packaging, Native AOT, mutation, benchmark, and release jobs | `ubuntu-24.04` |
| Lightweight workflow coordination and policy checks | `ubuntu-latest` |
| Visual Studio and .NET MAUI jobs | `windows-latest` |

`scripts/test-hosted-runner-policy.ps1` rejects dynamic runner expressions,
self-hosted labels, undeclared labels, and the removed runner-image/profile surfaces.

## Windows runner evidence

The [.NET MAUI workflow](https://github.com/ncosentino/needlr/blob/main/.github/workflows/maui-example.yml)
and the [IDE extension workflow](https://github.com/ncosentino/needlr/blob/main/.github/workflows/ide-extensions.yml)
use `windows-latest` for their Windows-specific jobs.

GitHub's
[standard runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners#standard-github-hosted-runners-for-public-repositories)
lists `windows-latest` in the Windows row for public repositories and states that
standard GitHub-hosted runner use is free and unlimited on public repositories.
GitHub's
[Actions billing documentation](https://docs.github.com/en/billing/concepts/product-billing/github-actions#free-use-of-github-actions)
independently confirms that standard GitHub-hosted runners are free in public
repositories and that larger runners are always charged.

Needlr therefore retains these Windows jobs while excluding GitHub-hosted larger
runners from its allowlist. The MAUI workflow runs manually or for MAUI-relevant
changes; the IDE extension workflow runs only when manually dispatched.

## Toolchain setup

`global.json` remains the exact .NET SDK contract. The shared
`.github/actions/setup-dotnet/action.yml` action uses an already-installed matching SDK
when available and otherwise installs that exact version with
`actions/setup-dotnet@v4`.

Native AOT jobs install their Linux prerequisites on the hosted worker before
publishing the example applications. Python and Node.js remain workflow-managed.

## Pull-request behavior

Draft pull requests use the subset declared by `CI_DRAFT_MODE`. Marking a pull request
ready starts the full validation set and publishes the stable `CI` check.

Guidance-only pull requests use the `guidance` scope. They run structural guidance,
documentation, and marketplace checks while skipping .NET build, package, and Native
AOT jobs. The stable required check remains `CI`; only its internal evidence changes.

Fork pull requests run within the same GitHub-hosted boundary as repository branches.
Workflow changes still require careful review because an approved fork run executes the
proposed workflow with the permissions GitHub grants to that event.

## Documentation deployment ownership

The main CI and release workflows both deploy to `gh-pages` with `keep_files: true`.
Each owns a disjoint generated slice and removes the other workflow's slice from its
local site output before deployment:

| Path | Owner |
| --- | --- |
| `/api/dev/*` | `ci.yml` |
| `/coverage/*` | `ci.yml` |
| `/api/stable/*` | `release.yml` |
| `/api/v<version>/*` | `release.yml` |
| Home, feature pages, and navigation | Both workflows, from identical sources |

Neither workflow writes generated API documentation back to `main`. Published
`/api/v<version>/` directories are immutable. The versioned API catalog is derived from
`git tag --list 'v*'`, not from ignored `docs/api/v*/` working-tree directories.

## Free-tier boundary

GitHub documents the Linux and Windows labels in Needlr's allowlist as standard
GitHub-hosted runners whose use is free and unlimited for public repositories. Larger
runners are always billed, so they are outside Needlr's declared runner policy.

Actions artifact storage is metered separately from runner minutes. Needlr keeps
explicit workflow artifacts for one day and does not use Actions caches. Public GitHub
Packages and the public Container registry are free under GitHub's current billing
policy, but storage and pricing remain external service constraints that should be
rechecked before expanding retention or changing package visibility.

## See also

- [ADR-0013: Use GitHub-hosted runners for public CI](adr/adr-0013-use-github-hosted-runners-for-public-ci.md)
- [Releasing Needlr](releasing.md)
