---
title: "ADR-0013: Use GitHub-hosted runners for public CI"
status: "Accepted"
date: "2026-08-21"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "ci", "runners", "github-actions"]
supersedes: "ADR-0010"
superseded_by: ""
---

## Context and scope

Needlr is a public GitHub repository. Its workflows nevertheless carried two
self-hosted execution paths: a general-purpose PitCrew fallback and a specialized
`needlr-ci` profile backed by a repository-owned GHCR image. The repository also used
`CI_RUNNER` to select those paths dynamically.

GitHub's billing documentation states that standard GitHub-hosted runners are free for
public repositories. Larger GitHub-hosted runners remain billable. Actions artifact
storage is metered separately, while public GitHub Packages and Container registry
usage are free under the current policy.

GitHub's standard public-runner table specifically lists `windows-latest` as a
standard Windows runner and states that standard hosted runner use is free and
unlimited on public repositories. Needlr requires that runner for its Visual Studio
VSIX builds and its Windows-targeted .NET MAUI example.

The self-hosted paths reduced repeated SDK and Native AOT setup, but they also required
image publication, digest activation, external capacity management, routing variables,
fork-specific trust logic, and repository validation dedicated to that infrastructure.
Those responsibilities no longer provide proportional value when standard hosted
capacity is available without runner-minute charges.

This decision governs Needlr's GitHub Actions runner selection, delivery manifest,
toolchain setup boundary, and short-lived workflow artifacts. It does not change the
repository's required checks, draft-versus-ready validation policy, release staging,
GitHub Pages publication, or package destinations.

## Decision drivers

- Public-repository CI should use the standard hosted capacity GitHub provides without
  runner-minute charges.
- Pull requests from forks and repository branches should share one managed execution
  boundary.
- Runner selection must be explicit and reviewable rather than controlled by a mutable
  repository variable.
- The exact .NET SDK and Native AOT prerequisites must remain reproducible.
- Larger billed runners must not enter the workflow accidentally.
- Actions artifact retention should remain bounded independently of free runner minutes.
- Removing self-hosted execution must also remove its image, profile, validation, and
  operational guidance.

## Decision

Needlr will run every GitHub Actions job on a standard GitHub-hosted runner declared
directly in the workflow.

Linux build, test, packaging, Native AOT, mutation, benchmark, documentation, and
release jobs use `ubuntu-24.04`. Lightweight coordination jobs may use
`ubuntu-latest`. Windows-specific IDE and MAUI jobs use `windows-latest`.

The delivery contract declares `github-hosted` as the runner provider and allowlists
the exact labels used by the repository. A repository test rejects dynamic `runs-on`
expressions, self-hosted labels, undeclared runner labels, and the former runner image
and profile surfaces.

`CI_RUNNER`, the PitCrew profile, repository-owned runner Dockerfile, image publication
workflow, and their dedicated validation scripts are removed. Needlr will not retain a
dormant self-hosted fallback.

`global.json` remains the exact SDK source of truth. The shared setup action installs
that SDK when the selected hosted image does not already contain it. Native AOT jobs
install `clang`, `file`, and `zlib1g-dev` before publication.

Explicit Actions artifacts use one-day retention. This bounds storage independently of
runner-minute pricing while preserving same-run transfer between staged jobs.

## Alternatives considered

### Keep the specialized PitCrew image

This avoids repeated SDK and Native AOT setup and provides controlled worker images.
It was rejected because it retains an external runner lifecycle, image publication,
digest rollout, and trust boundary solely to optimize setup that standard hosted
workers can perform within the existing workflow.

### Keep `CI_RUNNER` with a hosted default

This would preserve an easy route back to self-hosted capacity. It was rejected because
the mutable variable would keep runner selection outside reviewed workflow code and
would preserve dormant infrastructure and validation with no current requirement.

### Use GitHub-hosted larger runners

Larger runners could reduce long build or mutation durations. They were rejected
because GitHub bills them even for public repositories, and current jobs already have
bounded parallelism and timeouts on standard runners.

## Consequences

### Positive

- Needlr no longer owns runner registration, capacity, image publication, or rollout.
- Fork and internal pull requests use the same GitHub-managed execution boundary.
- Runner labels are visible in reviewed workflow files and checked against a committed
  allowlist.
- Public-repository standard runner minutes do not consume the private-repository
  minute allowance.
- Removing the GHCR runner image stops future image versions and publication artifacts.

### Negative

- Hosted jobs may spend additional time installing the exact SDK and Native AOT
  prerequisites.
- GitHub-hosted runner availability and image changes are external dependencies.
- A future need for specialized hardware or images requires a new decision rather than
  changing a repository variable.

### Neutral

- Required checks, draft validation, mutation bounds, release staging, and deployment
  permissions remain unchanged.
- Actions artifacts and package visibility remain separate billing concerns from
  standard runner minutes.
- Historical ADR and changelog entries continue to describe the superseded runner-image
  architecture.

## Confirmation

`scripts/test-hosted-runner-policy.ps1` validates the hosted provider declaration,
allowed labels, direct `runs-on` values, and absence of the removed self-hosted
surfaces. CI runs that test before build and package validation.

The pull request implementing this decision must complete its full checks on the
declared GitHub-hosted runners. Repository settings no longer need `CI_RUNNER` after
the hosted-only workflows reach the default branch.

## References

- ADR-0010 records the superseded repository-owned runner image and PitCrew profile
  decision.
- GitHub Actions billing documents free standard runner usage for public repositories
  and separate billing for larger runners and artifact storage:
  <https://docs.github.com/en/billing/concepts/product-billing/github-actions>.
- GitHub's hosted-runner reference lists `windows-latest` among the standard public
  runner labels and states that their use is free and unlimited:
  <https://docs.github.com/en/actions/reference/runners/github-hosted-runners#standard-github-hosted-runners-for-public-repositories>.
- GitHub Packages billing documents free public package usage and the current Container
  registry storage policy:
  <https://docs.github.com/en/billing/concepts/product-billing/github-packages>.
