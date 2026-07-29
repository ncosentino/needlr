---
title: "ADR-0010: Own a repository runner image"
status: "Accepted"
date: "2026-07-29"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "ci", "containers", "runners"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr's trusted Linux validation and release jobs use ephemeral PitCrew workers. Those
jobs repeatedly acquire the .NET SDK and install Native AOT prerequisites even though
the required toolchain changes much less frequently than the workers.

Release `v0.0.3-alpha.3` demonstrated the operational cost. Its publication worker spent
about twenty minutes in SDK setup before any release preparation could begin. Staged
release publication now makes downstream retries cheaper, but it does not make worker
startup deterministic.

Needlr already selects trusted Linux runners through `CI_RUNNER`, preserves a
general-purpose PitCrew fallback, and forces external forks onto GitHub-hosted workers.
PitCrew supports operator-approved external OCI profiles with immutable image identity,
verification commands, rolling worker replacement, and rollback.

This decision governs the toolchain image, publication boundary, profile identity, and
workflow routing for Needlr's trusted Linux jobs. It does not let repository workflows
mutate PitCrew, change host capacity, or select arbitrary images at runtime.

## Decision drivers

- Ephemeral workers should start with the exact supported .NET SDK already installed.
- Runner configuration must remain reproducible and rollback-safe.
- Pull requests must validate image changes without publishing deployable images.
- Only trusted `main` may publish the image.
- External forks must never reach self-hosted infrastructure.
- GitHub-hosted and general-purpose PitCrew fallbacks must remain available.
- No source, generated output, or credentials may enter an image layer.
- Host capacity and image activation remain operator-owned.

## Decision

Needlr will publish a public Linux amd64 runner image at
`ghcr.io/ncosentino/needlr-runner`.

GHCR creates the package as private on its first push. A repository owner performs a
one-time visibility change after trusted `main` publishes the initial image and verifies
anonymous digest access before the profile PR is opened. Later image revisions retain the
package's public visibility.

`global.json` is the source of truth for the exact .NET SDK. The runner Dockerfile uses
the same SDK version, pins every base image by SHA-256 digest, copies only the .NET
installation from the SDK stage, and installs the stable Native AOT OS prerequisites.
Python and Node.js remain workflow-managed until the repository defines exact versions
for them.

Pull requests build and execute the image on GitHub-hosted Ubuntu without publishing it.
Trusted `main` publishes a commit-tagged image and records the immutable manifest digest.
No pull-request event receives package publication capability.

Activation uses a separately reviewed `.pitcrew/runner-profile.json` named `needlr-ci`.
The profile references the immutable GHCR digest, disables GitHub default labels, and
verifies the runner listener, SDK, and advertised native tools. PitCrew automatically
adds `needlr-ci` as the routing label.

Bootstrap and updates use two repository changes: the image contract first, then the
published digest and routing contract. A workflow cannot safely commit the digest of an
image that trusted `main` has not published yet.

The host operator applies the profile with Needlr's existing capacity, confirms the
specialized runner is online, and only then sets `CI_RUNNER=needlr-ci`. Repository
workflows detect the exact SDK before invoking setup, so the specialized profile avoids
the download while hosted fallback remains functional.

## Alternatives considered

### Continue runtime SDK installation

This keeps workflows portable and avoids image ownership. It was rejected because every
ephemeral worker repeats slow external acquisition and can stall before useful work
begins.

### Put the toolchain in PitCrew's default image

This centralizes image maintenance. It was rejected because PitCrew is a runner
orchestrator, not an ecosystem toolchain catalog. Needlr owns its SDK and native build
requirements.

### Publish and activate a mutable tag

This simplifies updates because the profile never changes. It was rejected because the
same profile document could resolve to different bytes, weakening auditability and
rollback.

### Publish from pull requests

This would make the image digest available before merge. It was rejected because
untrusted or unreviewed code must not publish deployable runner images.

### Include Python and Node.js in the initial contract

The base runner already supplies those tools and workflows currently request floating
versions. Adding them without exact repository version contracts would create an
appearance of determinism without providing it. They remain workflow-managed.

## Consequences

### Positive

- The exact .NET SDK and Native AOT prerequisites are prepared once per image revision.
- Image changes are reviewed, validated, digest-pinned, and independently rollbackable.
- Hosted fallback and external-fork isolation remain intact.
- Needlr owns its toolchain lifecycle without expanding PitCrew's responsibilities.

### Negative

- Image publication and digest activation require two pull requests.
- Needlr assumes GHCR image maintenance and storage.
- The operator must apply the profile on each approved host before changing routing.
- SDK servicing requires coordinated changes to `global.json`, the image, and profile
  digest.

### Neutral

- Python and Node.js setup remain unchanged.
- Existing general-purpose PitCrew capacity remains available during rollout and
  rollback.
- Busy stale workers may continue until their current job finishes.

## Confirmation

Repository tests validate the SDK pin, base-image digests, absence of source or
credential-bearing Dockerfile inputs, trusted publication trigger, commit tag, and digest
capture.

The pull-request image workflow builds and executes the candidate. The trusted-main run
must publish a digest before the profile PR can be created.

Activation is confirmed by a live `needlr-ci` runner, representative source validation,
both Native AOT jobs, documentation generation, and a non-publishing release dry run.
PitCrew rollout evidence must report the approved target image and preserve the existing
Needlr capacity.

## References

- ADR-0009 records the staged release publication model whose preparation job benefits
  from deterministic worker startup.
- Needlr issue 119 defines the repository-owned image and portable routing requirements.
- PitCrew's repository-owned image guide defines the operator/repository ownership
  boundary and digest-pinned external profile lifecycle.
