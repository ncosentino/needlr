---
title: "ADR-0009: Stage release publication behind one prepared candidate"
status: "Accepted"
date: "2026-07-28"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "ci", "release", "packaging"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr publishes from a version tag. The tag workflow first confirms that the exact
tagged commit already completed a successful `main` CI run, which restores, builds,
runs the test suite, validates packages, and compiles both Native AOT samples. That
verification proves the commit is releasable.

Until this decision, the tag workflow then discarded the value of that proof. A single
publication job repeated restore, build, the full test suite, coverage generation,
packaging, and documentation generation, and only afterwards performed the irreversible
operations: pushing to NuGet.org, pushing to GitHub Packages, deploying documentation,
and creating the GitHub Release. The job declared no timeout. Release `v0.0.3-alpha.3`
demonstrated the cost: main CI run `30402851026` succeeded for the exact commit, then
release run `30406601340` began the entire validation sequence again, and a stalled
publication worker meant the only recovery was cancelling and repeating all of it.

This decision governs the staging, artifact contract, and retry model of
`.github/workflows/release.yml`. It does not change what a release contains, which
packages ship, how versions are validated, or the local gates in `scripts/release.ps1`.

## Decision drivers

- A transient failure in an irreversible publication step must not force recompilation
  or re-execution of tests that already passed for the same commit.
- Every reversible operation must complete before the first irreversible operation.
- Each publication destination must be independently retryable and idempotent.
- Anything published must be provably the artifact produced from the validated commit.
- Storage cost matters. Retaining artifacts between workflow runs must never become a
  requirement of the release contract.
- The published package set, release notes, documentation output, provenance, and
  permissions must not regress.

## Decision

`release.yml` is staged into one verification job, one reversible preparation job, and
four publication jobs.

`verify-main-ci` waits for the successful same-commit `main` CI run. `prepare` validates
the tag against that verified run, builds, packs, produces a digest manifest, extracts
release notes, builds the documentation site, and uploads two artifacts scoped to the
release run. `publish-nuget`, `publish-github-packages`, `deploy-documentation`, and
`create-release` each download the prepared artifacts, re-verify them, and perform
exactly one irreversible operation. No publication job restores, builds, or tests.

The release path no longer repeats the test suite or coverage generation. `verify-main-ci`
is the gate that proves the commit passed those checks, and duplicating them only widened
the window in which a runner failure destroyed validated work.

A release candidate is defined by `release-manifest.json`, written by
`scripts/write-release-manifest.ps1`. The manifest records the schema version, release
version, package version, source commit SHA, producing run identifier, producing workflow,
the identifier of the validated CI run, and a SHA-256 digest and size for every packaged
file. `scripts/verify-release-manifest.ps1` recomputes the digests and rejects a candidate
that is incomplete, tampered with, carries unlisted packages, or does not match the
version, commit, producing run, and validated CI run the publishing job expects.
`scripts/pack-release-packages.ps1` owns the published project selection so the package set
cannot drift between producers, and `scripts/test-release-artifacts.ps1` exercises the whole
contract during CI preflight.

Artifacts remain scoped to the release run. Retrying a destination replays only that
destination against artifacts the same run already produced.

## Alternatives considered

### Keep one monolithic publication job

The status quo needs no artifact contract and keeps the whole release readable in one
place. It was rejected because it makes every irreversible step depend on a fresh, full,
redundant validation sequence, so any late failure costs the entire release again. It also
leaves no way to retry a single destination.

### Promote a release candidate produced by main CI

Main CI could pack a candidate, upload it, and let the tag workflow download and publish
that exact bundle, giving the strongest provenance and the fastest possible tag workflow.
It was rejected as the default because it makes cross-run artifact retention a hard
requirement of the release contract and imposes storage cost on every main build, which is
an unacceptable steady-state expense for this repository. The saving is also smaller than
it appears: `scripts/generate-api-docs.sh` requires the Release build's XML documentation
output, so the release run must build regardless, and promoting packages alone would only
avoid `dotnet pack`.

The manifest format is deliberately producer-agnostic. It records the producing workflow
and the validated CI run separately, so an opt-in accelerator that promotes a candidate
from another run can be added later without changing the verification contract or making
retention mandatory.

### Split preparation and publication only for retries, keeping tests in the release path

Retaining the release-path test run would preserve the previous belt-and-braces validation
while still gaining restartability. It was rejected because the tests add no information
that `verify-main-ci` has not already established for the identical commit, while
lengthening the pre-publication window that the change is meant to shorten.

## Consequences

### Positive

- A failed destination is retried on its own; publication never rebuilds or retests.
- All validation completes before the first irreversible operation.
- Published artifacts are digest-bound to the validated commit and run.
- Every job has an explicit timeout and least-privilege permissions.
- Release duration drops by the length of the duplicated test and coverage run.

### Negative

- The release workflow is longer and has more moving parts than one job.
- Prepared artifacts occupy run-scoped storage for their retention window, and a retry
  attempted after expiry requires a full workflow re-run.
- The `release` environment now gates four jobs instead of one, so required reviewers, if
  ever configured, would prompt per destination.
- Release-path confidence now depends on `verify-main-ci` being correct about the commit.

### Neutral

- Documentation deployment still follows package publication, as in the previous
  sequential job, so documentation never announces a release whose packages failed.
- Coverage output is no longer produced by the release path. The documentation ownership
  contract already assigns `/coverage/*` to `ci.yml`, and `keep_files` preserves the copy
  published by the same commit's CI run.

## Confirmation

`scripts/test-release-artifacts.ps1` runs in CI preflight and asserts the published project
selection, the pack invocation contract, manifest creation, and every rejection case:
digest tampering, missing packages, unlisted packages, mismatched version, mismatched
commit, mismatched producing run, mismatched validated CI run, unsupported schema, and
expectations supplied without a value.

The workflow structure is confirmed by inspection and by `actionlint`: no publication job
contains a restore, build, or test step, and every publication job verifies the manifest
before its irreversible step.

Retry behavior against live registries cannot be confirmed from the repository. It is
observed on the first release that exercises a failed destination.

## References

- Issue "Release workflow rebuilds an already validated commit instead of promoting its
  artifacts" reports the duplicated validation and the resulting failed release attempt.
- `docs/releasing.md` documents the staged jobs, the candidate contract, and the retry
  procedure for maintainers.
- `.github/instructions/docs.instructions.md` defines the `gh-pages` path ownership split
  that keeps `ci.yml` responsible for `/api/dev/*` and `/coverage/*` while `release.yml`
  owns `/api/stable/*` and `/api/v<version>/*`.
