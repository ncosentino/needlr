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
    -Repos https://github.com/ncosentino/needlr=2
```

The workflows use the `CI_RUNNER` repository variable as PitCrew's manual
cloud fallback. Leave it unset for local ephemeral runners, or set it to
`ubuntu-latest` to route Linux jobs to GitHub-hosted runners.

Pull requests from forks always use `ubuntu-latest`; untrusted code must never
run on self-hosted infrastructure.

PitCrew workers are socketless Linux containers. Workloads requiring Docker,
service containers, Testcontainers, Windows, or macOS remain on an appropriate
hosted or isolated runner profile. Needlr's Windows and MAUI jobs therefore
continue using native GitHub-hosted runners.
