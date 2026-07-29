---
description: Build, publish, activate, update, and roll back Needlr's repository-owned PitCrew runner image.
---

# Repository-Owned Runner Image

Needlr owns a Linux amd64 GitHub Actions runner image containing the exact .NET
SDK and Native AOT prerequisites used by source validation and release preparation.
PitCrew remains responsible for runner registration, capacity, and worker lifecycle.

The image contains no repository source, generated output, registration token, package
credential, or deployment credential.

## Pinned Toolchain

`global.json` pins .NET SDK `10.0.302` with roll-forward disabled. The runner
Dockerfile uses the same SDK and pins both of its base images by immutable SHA-256
digest.

The image also includes the Linux packages required by Needlr's Native AOT jobs:

- `clang`;
- `file`;
- `zlib1g-dev`.

Python and Node.js remain workflow-managed. Their current workflow inputs are not exact
version contracts, and they were not the cause of repeated SDK acquisition.

## Publication

`.github/workflows/runner-image.yml` uses GitHub-hosted Ubuntu workers because image
validation requires Docker:

- pull requests build and run the candidate without publishing it;
- `main` publishes `ghcr.io/ncosentino/needlr-runner:sha-<commit>`;
- the workflow captures the immutable registry manifest digest and uploads a
  `needlr-runner-publication` record.

The initial trusted publication produced:

```text
ghcr.io/ncosentino/needlr-runner@sha256:233a06905fc35312fe73099d12e93bf496ac1b98dd19a0482be000c12d7b4461
```

Anonymous manifest retrieval succeeds for that digest. Workflows and PitCrew profiles
deploy by digest, never by a mutable tag.

## Bootstrap Sequence

Image publication and profile activation require two reviewed repository changes:

1. Merge the Dockerfile, SDK pin, tests, and publication workflow.
2. Read the immutable digest from the trusted `main` publication run.
3. Make the new GHCR package public and verify anonymous access to that digest.
4. Commit `.pitcrew/runner-profile.json` with that digest.
5. Merge the profile and portable workflow-routing changes.
6. Apply the approved external profile on the runner host.
7. Set `CI_RUNNER=needlr-ci` only after the specialized runner is online.

The digest cannot be safely committed before trusted `main` publishes the image, so the
two-PR sequence is intentional.

## Host Activation

PitCrew automatically adds the profile name as a routing label. A profile named
`needlr-ci` therefore satisfies jobs whose `runs-on` value is `needlr-ci`, even when
GitHub default labels are disabled.

Needlr's existing capacity is one worker. On the Zephyr host, run from the PitCrew
checkout:

```powershell
.\Setup-Runner.ps1 `
    -ProfilePath <needlr-checkout>\.pitcrew\runner-profile.json `
    -Repos https://github.com/ncosentino/needlr=1
```

This creates a separate `needlr-ci` manager and does not change the existing
general-purpose profile or Foundry capacity.

Confirm the specialized runner is online before changing repository routing:

```powershell
gh api repos/ncosentino/needlr/actions/runners `
    --jq '.runners[] | select(.labels[].name == "needlr-ci")'
```

Then route trusted Linux jobs:

```powershell
gh variable set CI_RUNNER `
    --repo ncosentino/needlr `
    --body needlr-ci
```

Run a full CI workflow and confirm `preflight`, build/test, package validation, both AOT
jobs, and documentation use a runner carrying the `needlr-ci` label. The conditional
setup action must report that SDK `10.0.302` is already installed.

Only after the specialized route is proven should the old default profile stop providing
Needlr capacity. Replay that profile with its existing non-Needlr repositories and
counts; do not change Foundry capacity.

Repository workflows never choose an OCI image or mutate PitCrew. Image activation is an
operator-approved host operation.

## Updating the Image

1. Change the Dockerfile or SDK pin through a pull request.
2. Let trusted `main` publish a new immutable digest.
3. Update the profile digest through a second pull request.
4. Apply the profile on one host with `pitcrew-profile-rollout`.
5. Verify the target image identity, current workers, stale workers, and rollout state.
6. Update other approved hosts after the first host is healthy.

`update.status: rolling` is successful partial convergence while busy ephemeral workers
finish naturally.

## Rollback

Restore the previous profile digest in a reviewed change and replay the complete external
profile command. Do not restart Docker, tear down the profile broadly, or use a mutable
image tag as a rollback target.

## Hosted Fallback

The existing `CI_RUNNER` contract remains portable:

- `needlr-ci` selects the specialized profile;
- `ubuntu-latest` selects GitHub-hosted Linux workers;
- an unset variable uses the existing general-purpose PitCrew fallback;
- external fork pull requests always route to `ubuntu-24.04` before the variable is
  considered.

## See Also

- [Local CI Runners](local-runners.md)
- [ADR-0010: Own a Repository Runner Image](adr/adr-0010-own-repository-runner-image.md)
