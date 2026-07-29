# Local CI Runners

Needlr routes trusted Linux jobs to
[PitCrew](https://github.com/ncosentino/pitcrew) using its default
`general-purpose` profile:

```yaml
runs-on: [self-hosted, linux, x64, general-purpose]
```

Provision repository-scoped capacity from a PitCrew checkout:

```powershell
.\Setup-Runner.ps1 `
    -Repos https://github.com/ncosentino/needlr=1
```

The workflows use the `CI_RUNNER` repository variable as PitCrew's manual
cloud fallback. Leave it unset for local ephemeral runners, or set it to
`ubuntu-latest` to route trusted Linux jobs to GitHub-hosted runners.

Pull requests from external forks always force `ubuntu-24.04` before
`CI_RUNNER` is considered; untrusted code must never run on self-hosted
infrastructure. Approving a fork workflow run authorizes the complete proposed
workflow, including runner selection, so inspect workflow changes before
approval.

Draft pull requests use `CI_DRAFT_MODE` (`subset` by default) and publish
`Draft CI` after marketplace validation and the delivery preflight. Making the
pull request ready starts fresh full validation and publishes the stable `CI`
check.

PitCrew workers are socketless Linux containers. Workloads requiring Docker,
service containers, Testcontainers, Windows, or macOS remain on an appropriate
hosted or isolated runner profile. Needlr's Windows and MAUI jobs therefore
continue using native GitHub-hosted runners.

Needlr also publishes a repository-owned `needlr-ci` image profile with the exact
.NET SDK and Native AOT prerequisites. See
[Repository-Owned Runner Image](runner-image.md) for publication, activation, update,
hosted fallback, and rollback. The committed profile pins the public GHCR digest and
preserves Needlr's existing capacity of one worker.
